namespace BlazorShop.Infrastructure.Services
{
    using System.Data;

    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Application.Services.Payment;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    using Npgsql;

    public sealed class PaymentTransactionStore : IPaymentTransactionStore
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public PaymentTransactionStore(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<PaymentTransaction> GetOrCreateStripeAsync(
            Guid orderId,
            long expectedAmountMinor,
            string currency,
            CancellationToken cancellationToken = default)
        {
            if (orderId == Guid.Empty)
            {
                throw new ArgumentException("A valid order is required.", nameof(orderId));
            }

            if (expectedAmountMinor < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedAmountMinor));
            }

            var normalizedCurrency = CurrencyMoney.NormalizeCurrency(currency);
            if (!CurrencyMoney.IsSupportedCurrency(normalizedCurrency))
            {
                throw new ArgumentOutOfRangeException(nameof(currency));
            }

            var transactionId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PaymentTransactions"
                    ("Id", "OrderId", "Provider", "ExpectedAmountMinor", "Currency", "Status", "CreatedOn", "UpdatedOn")
                VALUES
                    ({transactionId}, {orderId}, {PaymentProviderNames.Stripe}, {expectedAmountMinor}, {normalizedCurrency},
                     {PaymentTransactionStatus.Pending.ToString()}, {now}, {now})
                ON CONFLICT ("OrderId", "Provider") DO NOTHING;
                """, cancellationToken);

            var transaction = await db.PaymentTransactions
                .AsNoTracking()
                .SingleAsync(
                    item => item.OrderId == orderId && item.Provider == PaymentProviderNames.Stripe,
                    cancellationToken);
            if (transaction.ExpectedAmountMinor != expectedAmountMinor
                || !string.Equals(transaction.Currency, normalizedCurrency, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The existing Stripe payment transaction does not match the order's immutable amount and currency.");
            }

            return transaction;
        }

        public async Task<PaymentTransaction?> GetStripeByOrderIdAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.PaymentTransactions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.OrderId == orderId && item.Provider == PaymentProviderNames.Stripe,
                    cancellationToken);
        }

        public async Task<PaymentProviderIdentityPersistenceResult> PersistProviderIdentityAsync(
            Guid paymentTransactionId,
            string providerSessionId,
            string? providerPaymentIntentId,
            CancellationToken cancellationToken = default)
        {
            if (paymentTransactionId == Guid.Empty || string.IsNullOrWhiteSpace(providerSessionId))
            {
                throw new ArgumentException("A payment transaction and provider session are required.");
            }

            await using var strategyContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var executionStrategy = strategyContext.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(
                () => PersistProviderIdentityWithinTransactionAsync(
                    paymentTransactionId,
                    providerSessionId,
                    providerPaymentIntentId,
                    cancellationToken));
        }

        private async Task<PaymentProviderIdentityPersistenceResult> PersistProviderIdentityWithinTransactionAsync(
            Guid paymentTransactionId,
            string providerSessionId,
            string? providerPaymentIntentId,
            CancellationToken cancellationToken)
        {
            await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            var paymentTransaction = (await db.PaymentTransactions
                .FromSqlInterpolated($"SELECT * FROM \"PaymentTransactions\" WHERE \"Id\" = {paymentTransactionId} FOR UPDATE")
                .ToListAsync(cancellationToken))
                .SingleOrDefault();
            if (paymentTransaction is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new PaymentProviderIdentityPersistenceResult(
                    PaymentProviderIdentityPersistenceOutcome.NotFound,
                    null);
            }

            if (!string.Equals(paymentTransaction.Provider, PaymentProviderNames.Stripe, StringComparison.Ordinal)
                || (paymentTransaction.ProviderSessionId is not null
                    && !string.Equals(paymentTransaction.ProviderSessionId, providerSessionId, StringComparison.Ordinal))
                || (!string.IsNullOrWhiteSpace(providerPaymentIntentId)
                    && paymentTransaction.ProviderPaymentIntentId is not null
                    && !string.Equals(
                        paymentTransaction.ProviderPaymentIntentId,
                        providerPaymentIntentId,
                        StringComparison.Ordinal)))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new PaymentProviderIdentityPersistenceResult(
                    PaymentProviderIdentityPersistenceOutcome.Conflict,
                    paymentTransaction.Status);
            }

            var changed = paymentTransaction.ProviderSessionId is null
                || (paymentTransaction.ProviderPaymentIntentId is null
                    && !string.IsNullOrWhiteSpace(providerPaymentIntentId));
            paymentTransaction.ProviderSessionId ??= providerSessionId;
            if (!string.IsNullOrWhiteSpace(providerPaymentIntentId))
            {
                paymentTransaction.ProviderPaymentIntentId ??= providerPaymentIntentId;
            }

            if (!changed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new PaymentProviderIdentityPersistenceResult(
                    PaymentProviderIdentityPersistenceOutcome.AlreadyPersisted,
                    paymentTransaction.Status);
            }

            paymentTransaction.UpdatedOn = DateTime.UtcNow;
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new PaymentProviderIdentityPersistenceResult(
                    PaymentProviderIdentityPersistenceOutcome.Persisted,
                    paymentTransaction.Status);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            })
            {
                await transaction.RollbackAsync(cancellationToken);
                return new PaymentProviderIdentityPersistenceResult(
                    PaymentProviderIdentityPersistenceOutcome.Conflict,
                    paymentTransaction.Status);
            }
        }
    }
}
