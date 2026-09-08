namespace BlazorShop.Infrastructure.Services
{
    using System.Data;

    using BlazorShop.Application.Services.Payment;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    public sealed class StripePaymentReconciliationService : IStripePaymentReconciliationService
    {
        private const string CheckoutCompleted = "checkout.session.completed";
        private const string CheckoutAsyncPaymentSucceeded = "checkout.session.async_payment_succeeded";
        private const string CheckoutAsyncPaymentFailed = "checkout.session.async_payment_failed";
        private const string CheckoutExpired = "checkout.session.expired";

        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<StripePaymentReconciliationService> _logger;

        public StripePaymentReconciliationService(
            IDbContextFactory<AppDbContext> contextFactory,
            ILogger<StripePaymentReconciliationService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task<StripePaymentReconciliationOutcome> ReconcileAsync(
            StripeWebhookEventData providerEvent,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(providerEvent);

            await using var strategyContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var executionStrategy = strategyContext.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(
                () => ReconcileWithinTransactionAsync(providerEvent, cancellationToken));
        }

        private async Task<StripePaymentReconciliationOutcome> ReconcileWithinTransactionAsync(
            StripeWebhookEventData providerEvent,
            CancellationToken cancellationToken)
        {
            // Keep this lock order stable: provider-event claim, provider-session advisory lock,
            // payment transaction, order, reservation, then product/variant inventory.
            await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);
            var receivedOn = DateTime.UtcNow;
            var ledgerId = Guid.NewGuid();
            var claimed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PaymentProviderEvents"
                    ("Id", "Provider", "ProviderEventId", "EventType", "ProviderCreatedOn",
                     "ProcessingOutcome", "ReceivedOn")
                VALUES
                    ({ledgerId}, {PaymentProviderNames.Stripe}, {providerEvent.EventId}, {providerEvent.EventType},
                     {providerEvent.ProviderCreatedOn}, {PaymentProviderEventOutcome.Pending.ToString()}, {receivedOn})
                ON CONFLICT ("Provider", "ProviderEventId") DO NOTHING;
                """, cancellationToken);
            if (claimed == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation(
                    "Ignored duplicate Stripe event {StripeEventId}.",
                    providerEvent.EventId);
                return StripePaymentReconciliationOutcome.Duplicate;
            }

            if (!string.IsNullOrWhiteSpace(providerEvent.SessionId))
            {
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT pg_advisory_xact_lock(hashtextextended({providerEvent.SessionId}, 0));",
                    cancellationToken);
            }

            var ledger = await db.PaymentProviderEvents.SingleAsync(
                item => item.Id == ledgerId,
                cancellationToken);
            var paymentTransaction = await ResolveAndLockTransactionAsync(db, providerEvent, cancellationToken);
            if (paymentTransaction is null)
            {
                return await RejectAsync(
                    db,
                    transaction,
                    ledger,
                    providerEvent,
                    "No local Stripe payment transaction matches the signed event correlation values.",
                    cancellationToken);
            }

            ledger.PaymentTransactionId = paymentTransaction.Id;
            ledger.OrderId = paymentTransaction.OrderId;
            var order = (await db.Orders
                .FromSqlInterpolated($"SELECT * FROM \"Orders\" WHERE \"Id\" = {paymentTransaction.OrderId} FOR UPDATE")
                .ToListAsync(cancellationToken))
                .SingleOrDefault();
            if (order is null)
            {
                return await RejectAsync(
                    db,
                    transaction,
                    ledger,
                    providerEvent,
                    "The correlated local order does not exist.",
                    cancellationToken);
            }

            var identityFailure = ValidateOrderIdentity(providerEvent, paymentTransaction, order);
            if (identityFailure is not null)
            {
                return await RejectAsync(
                    db,
                    transaction,
                    ledger,
                    providerEvent,
                    identityFailure,
                    cancellationToken);
            }

            var monetaryFailure = ValidateMonetaryIdentity(providerEvent, paymentTransaction, order);
            if (monetaryFailure is not null)
            {
                return await RejectAsync(
                    db,
                    transaction,
                    ledger,
                    providerEvent,
                    monetaryFailure,
                    cancellationToken);
            }

            var target = ResolveTarget(providerEvent, paymentTransaction.ExpectedAmountMinor);
            if (target.RejectionReason is not null)
            {
                return await RejectAsync(
                    db,
                    transaction,
                    ledger,
                    providerEvent,
                    target.RejectionReason,
                    cancellationToken);
            }

            var providerIdentityFailure = await BindOrValidateProviderIdentityAsync(
                db,
                paymentTransaction,
                providerEvent,
                cancellationToken);
            if (providerIdentityFailure is not null)
            {
                return await RejectAsync(
                    db,
                    transaction,
                    ledger,
                    providerEvent,
                    providerIdentityFailure,
                    cancellationToken);
            }

            if (!target.Status.HasValue)
            {
                ledger.ProcessingOutcome = PaymentProviderEventOutcome.Ignored;
                ledger.FailureReason = target.IgnoreReason;
                ledger.ProcessedOn = DateTime.UtcNow;
                paymentTransaction.UpdatedOn = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation(
                    "Recorded Stripe event {StripeEventId} as ignored for transaction {PaymentTransactionId}: {Reason}",
                    providerEvent.EventId,
                    paymentTransaction.Id,
                    target.IgnoreReason);
                return StripePaymentReconciliationOutcome.Ignored;
            }

            if (paymentTransaction.Status == target.Status.Value)
            {
                CompleteLedger(ledger, PaymentProviderEventOutcome.Processed, "The payment state was already applied.");
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return StripePaymentReconciliationOutcome.Processed;
            }

            if (paymentTransaction.Status != PaymentTransactionStatus.Pending)
            {
                return await RejectAsync(
                    db,
                    transaction,
                    ledger,
                    providerEvent,
                    $"Illegal payment transition {paymentTransaction.Status} -> {target.Status.Value}.",
                    cancellationToken);
            }

            var (orderStatus, reservationStatus) = target.Status.Value switch
            {
                PaymentTransactionStatus.Paid =>
                    (PaymentOrderStatus.Paid, InventoryReservationStatus.Consumed),
                PaymentTransactionStatus.Failed =>
                    (PaymentOrderStatus.PaymentFailed, InventoryReservationStatus.Released),
                PaymentTransactionStatus.Cancelled =>
                    (PaymentOrderStatus.Cancelled, InventoryReservationStatus.Released),
                _ => throw new InvalidOperationException("Pending is not a terminal provider transition."),
            };
            var inventoryResult = await InventoryReservationTransitionOperation.ApplyAsync(
                db,
                order,
                orderStatus,
                reservationStatus,
                cancellationToken);
            if (inventoryResult.Outcome != InventoryTransitionOutcome.Applied)
            {
                return await RejectAsync(
                    db,
                    transaction,
                    ledger,
                    providerEvent,
                    inventoryResult.ErrorMessage
                    ?? $"Inventory transition was not legal: {inventoryResult.Outcome}.",
                    cancellationToken);
            }

            ApplyPaymentStatus(paymentTransaction, target.Status.Value);
            CompleteLedger(ledger, PaymentProviderEventOutcome.Processed, null);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation(
                "Reconciled Stripe event {StripeEventId} for transaction {PaymentTransactionId}, order {OrderId}, amount {AmountMinor} {Currency}, status {PaymentStatus}.",
                providerEvent.EventId,
                paymentTransaction.Id,
                order.Id,
                paymentTransaction.ExpectedAmountMinor,
                paymentTransaction.Currency,
                paymentTransaction.Status);
            return StripePaymentReconciliationOutcome.Processed;
        }

        private static async Task<PaymentTransaction?> ResolveAndLockTransactionAsync(
            AppDbContext db,
            StripeWebhookEventData providerEvent,
            CancellationToken cancellationToken)
        {
            if (providerEvent.PaymentTransactionId.HasValue)
            {
                return (await db.PaymentTransactions
                    .FromSqlInterpolated($"SELECT * FROM \"PaymentTransactions\" WHERE \"Id\" = {providerEvent.PaymentTransactionId.Value} AND \"Provider\" = {PaymentProviderNames.Stripe} FOR UPDATE")
                    .ToListAsync(cancellationToken))
                    .SingleOrDefault();
            }

            if (!string.IsNullOrWhiteSpace(providerEvent.SessionId))
            {
                return (await db.PaymentTransactions
                    .FromSqlInterpolated($"SELECT * FROM \"PaymentTransactions\" WHERE \"Provider\" = {PaymentProviderNames.Stripe} AND \"ProviderSessionId\" = {providerEvent.SessionId} FOR UPDATE")
                    .ToListAsync(cancellationToken))
                    .SingleOrDefault();
            }

            if (!string.IsNullOrWhiteSpace(providerEvent.PaymentIntentId))
            {
                return (await db.PaymentTransactions
                    .FromSqlInterpolated($"SELECT * FROM \"PaymentTransactions\" WHERE \"Provider\" = {PaymentProviderNames.Stripe} AND \"ProviderPaymentIntentId\" = {providerEvent.PaymentIntentId} FOR UPDATE")
                    .ToListAsync(cancellationToken))
                    .SingleOrDefault();
            }

            return null;
        }

        private static string? ValidateOrderIdentity(
            StripeWebhookEventData providerEvent,
            PaymentTransaction paymentTransaction,
            Order order)
        {
            if (providerEvent.PaymentTransactionId.HasValue
                && providerEvent.PaymentTransactionId.Value != paymentTransaction.Id)
            {
                return "payment_transaction_id metadata does not match the resolved payment transaction.";
            }

            if (providerEvent.OrderId.HasValue && providerEvent.OrderId.Value != paymentTransaction.OrderId)
            {
                return "order_id metadata does not match the payment transaction order.";
            }

            if (!string.IsNullOrWhiteSpace(providerEvent.ClientReferenceId)
                && (!Guid.TryParse(providerEvent.ClientReferenceId, out var clientOrderId)
                    || clientOrderId != paymentTransaction.OrderId))
            {
                return "Stripe ClientReferenceId does not match the payment transaction order.";
            }

            return order.Id != paymentTransaction.OrderId
                ? "The payment transaction and order identities disagree."
                : null;
        }

        private static string? ValidateMonetaryIdentity(
            StripeWebhookEventData providerEvent,
            PaymentTransaction paymentTransaction,
            Order order)
        {
            if (!string.Equals(order.Currency, paymentTransaction.Currency, StringComparison.Ordinal))
            {
                return "The immutable order and payment transaction currencies disagree.";
            }

            long orderAmountMinor;
            try
            {
                orderAmountMinor = CurrencyMoney.ToMinorUnits(order.TotalAmount, order.Currency);
            }
            catch (Exception exception) when (exception is ArgumentOutOfRangeException or OverflowException)
            {
                return "The immutable order amount cannot be represented in its currency.";
            }

            if (orderAmountMinor != paymentTransaction.ExpectedAmountMinor)
            {
                return "The immutable order total and payment transaction expected amount disagree.";
            }

            if (providerEvent.AmountTotal.HasValue
                && providerEvent.AmountTotal.Value != paymentTransaction.ExpectedAmountMinor)
            {
                return $"Stripe amount mismatch: expected {paymentTransaction.ExpectedAmountMinor}, received {providerEvent.AmountTotal.Value}.";
            }

            if (!string.IsNullOrWhiteSpace(providerEvent.Currency)
                && !string.Equals(
                    CurrencyMoney.NormalizeCurrency(providerEvent.Currency),
                    paymentTransaction.Currency,
                    StringComparison.Ordinal))
            {
                return $"Stripe currency mismatch: expected {paymentTransaction.Currency}, received {CurrencyMoney.NormalizeCurrency(providerEvent.Currency)}.";
            }

            return null;
        }

        private static async Task<string?> BindOrValidateProviderIdentityAsync(
            AppDbContext db,
            PaymentTransaction paymentTransaction,
            StripeWebhookEventData providerEvent,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(providerEvent.SessionId))
            {
                return "A recognized Stripe Checkout Session event has no SessionId.";
            }

            var bindSession = paymentTransaction.ProviderSessionId is null;
            if (!bindSession)
            {
                if (!string.Equals(
                    paymentTransaction.ProviderSessionId,
                    providerEvent.SessionId,
                    StringComparison.Ordinal))
                {
                    return "Stripe SessionId does not match the bound provider session.";
                }
            }
            else
            {
                if (providerEvent.PaymentTransactionId != paymentTransaction.Id
                    || providerEvent.OrderId != paymentTransaction.OrderId
                    || providerEvent.AmountTotal != paymentTransaction.ExpectedAmountMinor
                    || !string.Equals(
                        CurrencyMoney.NormalizeCurrency(providerEvent.Currency),
                        paymentTransaction.Currency,
                        StringComparison.Ordinal))
                {
                    return "An unbound Stripe session did not provide all matching transaction, order, amount, and currency values.";
                }

                var conflictingSession = await db.PaymentTransactions.AsNoTracking().AnyAsync(
                    item => item.Id != paymentTransaction.Id
                        && item.Provider == PaymentProviderNames.Stripe
                        && item.ProviderSessionId == providerEvent.SessionId,
                    cancellationToken);
                if (conflictingSession)
                {
                    return "Stripe SessionId is already bound to another payment transaction.";
                }

            }

            var bindPaymentIntent = false;
            if (!string.IsNullOrWhiteSpace(providerEvent.PaymentIntentId))
            {
                if (paymentTransaction.ProviderPaymentIntentId is not null
                    && !string.Equals(
                        paymentTransaction.ProviderPaymentIntentId,
                        providerEvent.PaymentIntentId,
                        StringComparison.Ordinal))
                {
                    return "Stripe PaymentIntentId does not match the bound provider payment intent.";
                }

                if (paymentTransaction.ProviderPaymentIntentId is null)
                {
                    var conflictingIntent = await db.PaymentTransactions.AsNoTracking().AnyAsync(
                        item => item.Id != paymentTransaction.Id
                            && item.Provider == PaymentProviderNames.Stripe
                            && item.ProviderPaymentIntentId == providerEvent.PaymentIntentId,
                        cancellationToken);
                    if (conflictingIntent)
                    {
                        return "Stripe PaymentIntentId is already bound to another payment transaction.";
                    }

                    bindPaymentIntent = true;
                }
            }

            if (bindSession)
            {
                paymentTransaction.ProviderSessionId = providerEvent.SessionId;
            }

            if (bindPaymentIntent)
            {
                paymentTransaction.ProviderPaymentIntentId = providerEvent.PaymentIntentId;
            }

            paymentTransaction.UpdatedOn = DateTime.UtcNow;
            return null;
        }

        private static PaymentTarget ResolveTarget(StripeWebhookEventData providerEvent, long expectedAmountMinor)
        {
            if (string.Equals(providerEvent.EventType, CheckoutCompleted, StringComparison.Ordinal))
            {
                if (string.Equals(providerEvent.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
                {
                    return RequirePaidMoney(providerEvent, PaymentTransactionStatus.Paid);
                }

                if (string.Equals(
                    providerEvent.PaymentStatus,
                    "no_payment_required",
                    StringComparison.OrdinalIgnoreCase))
                {
                    return expectedAmountMinor == 0
                        ? RequirePaidMoney(providerEvent, PaymentTransactionStatus.Paid)
                        : PaymentTarget.Rejected(
                            "no_payment_required cannot pay a non-zero payment transaction.");
                }

                return PaymentTarget.Ignored(
                    $"checkout.session.completed is not paid (payment_status={providerEvent.PaymentStatus ?? "missing"}).");
            }

            if (string.Equals(providerEvent.EventType, CheckoutAsyncPaymentSucceeded, StringComparison.Ordinal))
            {
                if (string.Equals(
                    providerEvent.PaymentStatus,
                    "no_payment_required",
                    StringComparison.OrdinalIgnoreCase)
                    && expectedAmountMinor != 0)
                {
                    return PaymentTarget.Rejected(
                        "no_payment_required cannot pay a non-zero payment transaction.");
                }

                return RequirePaidMoney(providerEvent, PaymentTransactionStatus.Paid);
            }

            if (string.Equals(providerEvent.EventType, CheckoutAsyncPaymentFailed, StringComparison.Ordinal))
            {
                return PaymentTarget.Transition(PaymentTransactionStatus.Failed);
            }

            return string.Equals(providerEvent.EventType, CheckoutExpired, StringComparison.Ordinal)
                ? PaymentTarget.Transition(PaymentTransactionStatus.Cancelled)
                : PaymentTarget.Ignored("The signed Stripe event type is not handled.");
        }

        private static PaymentTarget RequirePaidMoney(
            StripeWebhookEventData providerEvent,
            PaymentTransactionStatus status) =>
            !providerEvent.AmountTotal.HasValue || string.IsNullOrWhiteSpace(providerEvent.Currency)
                ? PaymentTarget.Rejected("A paid Stripe event must contain AmountTotal and Currency.")
                : PaymentTarget.Transition(status);

        private static void ApplyPaymentStatus(
            PaymentTransaction paymentTransaction,
            PaymentTransactionStatus status)
        {
            var now = DateTime.UtcNow;
            paymentTransaction.Status = status;
            paymentTransaction.UpdatedOn = now;
            paymentTransaction.PaidOn = status == PaymentTransactionStatus.Paid ? now : null;
            paymentTransaction.FailedOn = status == PaymentTransactionStatus.Failed ? now : null;
            paymentTransaction.CancelledOn = status == PaymentTransactionStatus.Cancelled ? now : null;
        }

        private async Task<StripePaymentReconciliationOutcome> RejectAsync(
            AppDbContext db,
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
            PaymentProviderEvent ledger,
            StripeWebhookEventData providerEvent,
            string reason,
            CancellationToken cancellationToken)
        {
            CompleteLedger(ledger, PaymentProviderEventOutcome.Rejected, reason);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogWarning(
                "Rejected Stripe event {StripeEventId} for transaction {PaymentTransactionId} and order {OrderId}: {Reason}",
                providerEvent.EventId,
                ledger.PaymentTransactionId,
                ledger.OrderId,
                reason);
            return StripePaymentReconciliationOutcome.Rejected;
        }

        private static void CompleteLedger(
            PaymentProviderEvent ledger,
            PaymentProviderEventOutcome outcome,
            string? reason)
        {
            ledger.ProcessingOutcome = outcome;
            ledger.FailureReason = reason;
            ledger.ProcessedOn = DateTime.UtcNow;
        }

        private sealed record PaymentTarget(
            PaymentTransactionStatus? Status,
            string? RejectionReason,
            string? IgnoreReason)
        {
            public static PaymentTarget Transition(PaymentTransactionStatus status) => new(status, null, null);

            public static PaymentTarget Rejected(string reason) => new(null, reason, null);

            public static PaymentTarget Ignored(string reason) => new(null, null, reason);
        }
    }
}
