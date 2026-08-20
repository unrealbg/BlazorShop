namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Domain.Entities.Payment;

    public interface ICheckoutIdempotencyStore
    {
        Task<CheckoutIdempotencyClaim> ClaimAsync(
            string userId,
            Guid idempotencyKey,
            string requestFingerprint,
            Guid paymentMethodId,
            CancellationToken cancellationToken = default);

        Task<bool> SetPendingOutcomeAsync(
            Guid recordId,
            Guid leaseOwnerId,
            PersistedCheckoutOutcome outcome,
            CancellationToken cancellationToken = default);

        Task<CheckoutProviderInitialization?> PrepareProviderInitializationAsync(
            Guid recordId,
            Guid leaseOwnerId,
            StripeCheckoutInitialization proposedInitialization,
            CancellationToken cancellationToken = default);

        Task<bool> CompleteAsync(
            Guid recordId,
            Guid leaseOwnerId,
            PersistedCheckoutOutcome outcome,
            CheckoutIdempotencyState terminalState,
            CancellationToken cancellationToken = default);

        Task<bool> ReleaseLeaseAsync(
            Guid recordId,
            Guid leaseOwnerId,
            CancellationToken cancellationToken = default);

        Task<CheckoutIdempotencyRecord?> GetAsync(
            Guid recordId,
            CancellationToken cancellationToken = default);

        PersistedCheckoutOutcome? ReadOutcome(CheckoutIdempotencyRecord record);
    }
}
