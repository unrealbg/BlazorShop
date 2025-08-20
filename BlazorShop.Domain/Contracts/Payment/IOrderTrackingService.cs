namespace BlazorShop.Domain.Contracts.Payment
{
    using BlazorShop.Domain.Entities.Payment;

    public interface IOrderTrackingService
    {
        Task UpdateTrackingAsync(Guid orderId, string carrier, string trackingNumber, string trackingUrl);

        Task UpdateShippingStatusAsync(Guid orderId, string shippingStatus, DateTime? shippedOn = null, DateTime? deliveredOn = null);
    }
}
