namespace BlazorShop.Application.DTOs.Payment
{
    using BlazorShop.Domain.Entities.Payment;

    public enum CheckoutIdempotencyClaimStatus
    {
        Acquired,
        Replayed,
        Conflict,
        InProgress,
    }

    public sealed record CheckoutIdempotencyClaim(
        CheckoutIdempotencyClaimStatus Status,
        CheckoutIdempotencyRecord Record,
        Guid LeaseOwnerId,
        PersistedCheckoutOutcome? Outcome = null);
}
