namespace BlazorShop.Domain.Contracts.Payment
{
    using BlazorShop.Domain.Entities.Payment;

    public interface IInventoryReservationService
    {
        Task<InventoryReservationResult> CreateOrderWithInventoryAsync(
            Order order,
            InventoryReservationStatus reservationStatus,
            CancellationToken cancellationToken = default);

        Task<InventoryTransitionResult> TransitionOrderAsync(
            Guid orderId,
            string orderStatus,
            InventoryReservationStatus reservationStatus,
            CancellationToken cancellationToken = default);
    }

    public sealed record InventoryReservationResult(bool Success, string? ErrorMessage = null);

    public sealed record InventoryTransitionResult(InventoryTransitionOutcome Outcome, string? ErrorMessage = null)
    {
        public bool Success => Outcome is InventoryTransitionOutcome.Applied or InventoryTransitionOutcome.AlreadyApplied;
    }

    public enum InventoryTransitionOutcome
    {
        Applied,
        AlreadyApplied,
        OrderNotFound,
        InvalidTransition,
    }
}
