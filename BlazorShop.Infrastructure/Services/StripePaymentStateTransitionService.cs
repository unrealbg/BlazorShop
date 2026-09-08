namespace BlazorShop.Infrastructure.Services
{
    using System.Data;
    using System.Text.Json;

    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;

    public sealed class StripePaymentStateTransitionService : IStripePaymentStateTransitionService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<StripePaymentStateTransitionService> _logger;

        public StripePaymentStateTransitionService(
            IDbContextFactory<AppDbContext> contextFactory,
            ILogger<StripePaymentStateTransitionService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        public async Task<StripePaymentStateTransitionResult> TransitionAsync(
            Guid paymentTransactionId,
            PaymentTransactionStatus targetStatus,
            CancellationToken cancellationToken = default)
        {
            if (paymentTransactionId == Guid.Empty)
            {
                return new StripePaymentStateTransitionResult(
                    StripePaymentStateTransitionOutcome.NotFound,
                    null,
                    "A valid Stripe payment transaction is required.");
            }

            if (targetStatus is not (PaymentTransactionStatus.Paid
                or PaymentTransactionStatus.Failed
                or PaymentTransactionStatus.Cancelled))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetStatus),
                    targetStatus,
                    "Stripe terminalization supports only Paid, Failed, or Cancelled.");
            }

            if (targetStatus == PaymentTransactionStatus.Failed)
            {
                return new StripePaymentStateTransitionResult(
                    StripePaymentStateTransitionOutcome.Conflict,
                    null,
                    "Local Stripe failure requires a fenced definitive-initial-rejection decision.");
            }

            await using var strategyContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var executionStrategy = strategyContext.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(
                () => TransitionWithinTransactionAsync(
                    paymentTransactionId,
                    targetStatus,
                    null,
                    cancellationToken));
        }

        public async Task<StripePaymentStateTransitionResult> TransitionDefinitiveInitialFailureAsync(
            Guid paymentTransactionId,
            Guid checkoutIdempotencyRecordId,
            Guid leaseOwnerId,
            Guid definitiveInitialRejectionId,
            CancellationToken cancellationToken = default)
        {
            if (paymentTransactionId == Guid.Empty
                || checkoutIdempotencyRecordId == Guid.Empty
                || leaseOwnerId == Guid.Empty
                || definitiveInitialRejectionId == Guid.Empty)
            {
                return new StripePaymentStateTransitionResult(
                    StripePaymentStateTransitionOutcome.Conflict,
                    null,
                    "A valid checkout lease and definitive rejection decision are required.");
            }

            await using var strategyContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var executionStrategy = strategyContext.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(
                () => TransitionWithinTransactionAsync(
                    paymentTransactionId,
                    PaymentTransactionStatus.Failed,
                    new DefinitiveInitialFailureAuthorization(
                        checkoutIdempotencyRecordId,
                        leaseOwnerId,
                        definitiveInitialRejectionId),
                    cancellationToken));
        }

        private async Task<StripePaymentStateTransitionResult> TransitionWithinTransactionAsync(
            Guid paymentTransactionId,
            PaymentTransactionStatus targetStatus,
            DefinitiveInitialFailureAuthorization? definitiveFailureAuthorization,
            CancellationToken cancellationToken)
        {
            await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted,
                cancellationToken);

            // Match webhook lock order after its event/session-specific locks:
            // payment transaction, optional checkout decision fence, order, reservation,
            // then product/variant inventory.
            var paymentTransaction = (await db.PaymentTransactions
                .FromSqlInterpolated($"SELECT * FROM \"PaymentTransactions\" WHERE \"Id\" = {paymentTransactionId} AND \"Provider\" = {PaymentProviderNames.Stripe} FOR UPDATE")
                .ToListAsync(cancellationToken))
                .SingleOrDefault();
            if (paymentTransaction is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new StripePaymentStateTransitionResult(
                    StripePaymentStateTransitionOutcome.NotFound,
                    null,
                    "The Stripe payment transaction does not exist.");
            }

            if (paymentTransaction.Status != PaymentTransactionStatus.Pending)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new StripePaymentStateTransitionResult(
                    StripePaymentStateTransitionOutcome.AlreadyTerminal,
                    paymentTransaction.Status);
            }

            if (definitiveFailureAuthorization is not null)
            {
                var authorizationFailure = await ValidateDefinitiveInitialFailureAuthorizationAsync(
                    db,
                    paymentTransaction,
                    definitiveFailureAuthorization,
                    cancellationToken);
                if (authorizationFailure is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return authorizationFailure;
                }
            }

            var order = (await db.Orders
                .FromSqlInterpolated($"SELECT * FROM \"Orders\" WHERE \"Id\" = {paymentTransaction.OrderId} FOR UPDATE")
                .ToListAsync(cancellationToken))
                .SingleOrDefault();
            if (order is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new StripePaymentStateTransitionResult(
                    StripePaymentStateTransitionOutcome.Conflict,
                    paymentTransaction.Status,
                    "The Stripe payment transaction's order does not exist.");
            }

            var (orderStatus, reservationStatus) = targetStatus switch
            {
                PaymentTransactionStatus.Paid =>
                    (PaymentOrderStatus.Paid, InventoryReservationStatus.Consumed),
                PaymentTransactionStatus.Failed =>
                    (PaymentOrderStatus.PaymentFailed, InventoryReservationStatus.Released),
                PaymentTransactionStatus.Cancelled =>
                    (PaymentOrderStatus.Cancelled, InventoryReservationStatus.Released),
                _ => throw new InvalidOperationException("Unsupported local Stripe terminal state."),
            };
            var inventoryResult = await InventoryReservationTransitionOperation.ApplyAsync(
                db,
                order,
                orderStatus,
                reservationStatus,
                cancellationToken);
            if (inventoryResult.Outcome != InventoryTransitionOutcome.Applied)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning(
                    "Refused coordinated Stripe transition {PaymentTransactionId} from Pending to {TargetStatus} because order/inventory state returned {InventoryOutcome}: {Reason}",
                    paymentTransaction.Id,
                    targetStatus,
                    inventoryResult.Outcome,
                    inventoryResult.ErrorMessage);
                return new StripePaymentStateTransitionResult(
                    StripePaymentStateTransitionOutcome.Conflict,
                    paymentTransaction.Status,
                    inventoryResult.ErrorMessage
                    ?? $"The coordinated inventory transition returned {inventoryResult.Outcome}.");
            }

            ApplyTerminalStatus(paymentTransaction, targetStatus);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation(
                "Applied coordinated Stripe transition {PaymentTransactionId} for order {OrderId}: payment {PaymentStatus}, order {OrderStatus}, inventory {InventoryStatus}.",
                paymentTransaction.Id,
                order.Id,
                paymentTransaction.Status,
                order.Status,
                reservationStatus);
            return new StripePaymentStateTransitionResult(
                StripePaymentStateTransitionOutcome.Applied,
                paymentTransaction.Status);
        }

        private static async Task<StripePaymentStateTransitionResult?>
            ValidateDefinitiveInitialFailureAuthorizationAsync(
                AppDbContext db,
                PaymentTransaction paymentTransaction,
                DefinitiveInitialFailureAuthorization authorization,
                CancellationToken cancellationToken)
        {
            // PaymentTransaction is locked first. The checkout decision is then fenced under
            // the same transaction before Order and inventory rows are locked or mutated.
            var checkoutRecord = (await db.CheckoutIdempotencyRecords
                .FromSqlInterpolated($"SELECT * FROM \"CheckoutIdempotencyRecords\" WHERE \"Id\" = {authorization.CheckoutIdempotencyRecordId} FOR UPDATE")
                .ToListAsync(cancellationToken))
                .SingleOrDefault();
            if (checkoutRecord is null
                || checkoutRecord.OrderId != paymentTransaction.OrderId
                || checkoutRecord.PaymentMethodId != PaymentMethodIds.CreditCard
                || checkoutRecord.LeaseOwnerId != authorization.LeaseOwnerId
                || checkoutRecord.State is not (CheckoutIdempotencyState.Processing
                    or CheckoutIdempotencyState.LocalCommitted)
                || !string.IsNullOrWhiteSpace(paymentTransaction.ProviderSessionId)
                || !string.IsNullOrWhiteSpace(paymentTransaction.ProviderPaymentIntentId))
            {
                return new StripePaymentStateTransitionResult(
                    StripePaymentStateTransitionOutcome.Conflict,
                    paymentTransaction.Status,
                    "The checkout lease or provider state no longer authorizes local failure compensation.");
            }

            PersistedCheckoutOutcome? outcome;
            try
            {
                outcome = string.IsNullOrWhiteSpace(checkoutRecord.OutcomeJson)
                    ? null
                    : JsonSerializer.Deserialize<PersistedCheckoutOutcome>(
                        checkoutRecord.OutcomeJson,
                        SerializerOptions);
            }
            catch (JsonException)
            {
                outcome = null;
            }

            if (outcome?.Status != CheckoutExecutionStatus.BadRequest
                || outcome.DefinitiveInitialRejectionId != authorization.DefinitiveInitialRejectionId)
            {
                return new StripePaymentStateTransitionResult(
                    StripePaymentStateTransitionOutcome.Conflict,
                    paymentTransaction.Status,
                    "The durable definitive-rejection decision is missing or was superseded.");
            }

            return null;
        }

        private static void ApplyTerminalStatus(
            PaymentTransaction paymentTransaction,
            PaymentTransactionStatus targetStatus)
        {
            var now = DateTime.UtcNow;
            paymentTransaction.Status = targetStatus;
            paymentTransaction.UpdatedOn = now;
            paymentTransaction.PaidOn = targetStatus == PaymentTransactionStatus.Paid ? now : null;
            paymentTransaction.FailedOn = targetStatus == PaymentTransactionStatus.Failed ? now : null;
            paymentTransaction.CancelledOn = targetStatus == PaymentTransactionStatus.Cancelled ? now : null;
        }

        private sealed record DefinitiveInitialFailureAuthorization(
            Guid CheckoutIdempotencyRecordId,
            Guid LeaseOwnerId,
            Guid DefinitiveInitialRejectionId);
    }
}
