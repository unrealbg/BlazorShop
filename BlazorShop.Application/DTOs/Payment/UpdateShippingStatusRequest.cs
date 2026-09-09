namespace BlazorShop.Application.DTOs.Payment
{
    public class UpdateShippingStatusRequest
    {
        public string FulfillmentStatus { get; set; } = string.Empty;

        public DateTime? ShippedOn { get; set; }

        public DateTime? DeliveredOn { get; set; }
    }
}
