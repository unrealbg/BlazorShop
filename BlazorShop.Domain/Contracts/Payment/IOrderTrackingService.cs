namespace BlazorShop.Domain.Contracts.Payment
{
    using BlazorShop.Domain.Entities.Payment;

    public interface IOrderTrackingService
    {
        Task<bool> UpdateTrackingAsync(Guid orderId, string carrier, string trackingNumber, string trackingUrl);

        Task<bool> UpdateShippingStatusAsync(Guid orderId, string shippingStatus, DateTime? shippedOn = null, DateTime? deliveredOn = null);

        Task<OrderTrackingTransitionResult> UpdateTrackingDetailsAsync(
            Guid orderId,
            string carrier,
            string trackingNumber,
            string trackingUrl,
            CancellationToken cancellationToken = default);

        Task<OrderTrackingTransitionResult> TransitionFulfillmentAsync(
            Guid orderId,
            string fulfillmentStatus,
            DateTime? shippedOn = null,
            DateTime? deliveredOn = null,
            CancellationToken cancellationToken = default);
    }

    public sealed record OrderTrackingTransitionResult(
        OrderTrackingTransitionOutcome Outcome,
        string? ErrorMessage = null)
    {
        public bool Success => Outcome is OrderTrackingTransitionOutcome.Applied
            or OrderTrackingTransitionOutcome.AlreadyApplied;
    }

    public enum OrderTrackingTransitionOutcome
    {
        Applied,
        AlreadyApplied,
        NotFound,
        ValidationError,
        Conflict,
    }
}
