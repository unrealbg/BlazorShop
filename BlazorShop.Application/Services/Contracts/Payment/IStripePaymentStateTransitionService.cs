namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Domain.Entities.Payment;

    public interface IStripePaymentStateTransitionService
    {
        Task<StripePaymentStateTransitionResult> TransitionAsync(
            Guid paymentTransactionId,
            PaymentTransactionStatus targetStatus,
            CancellationToken cancellationToken = default);

        Task<StripePaymentStateTransitionResult> TransitionDefinitiveInitialFailureAsync(
            Guid paymentTransactionId,
            Guid checkoutIdempotencyRecordId,
            Guid leaseOwnerId,
            Guid definitiveInitialRejectionId,
            CancellationToken cancellationToken = default);
    }

    public sealed record StripePaymentStateTransitionResult(
        StripePaymentStateTransitionOutcome Outcome,
        PaymentTransactionStatus? PaymentStatus,
        string? ErrorMessage = null);

    public enum StripePaymentStateTransitionOutcome
    {
        Applied,
        AlreadyTerminal,
        NotFound,
        Conflict,
    }
}
