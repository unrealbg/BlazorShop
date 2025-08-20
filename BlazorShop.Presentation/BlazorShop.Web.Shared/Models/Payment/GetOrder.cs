namespace BlazorShop.Web.Shared.Models.Payment
{
    public class GetOrder
    {
        public Guid Id { get; set; }

        public string Reference { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public DateTime CreatedOn { get; set; }

        public string ShippingStatus { get; set; } = string.Empty;

        public string? ShippingCarrier { get; set; }

        public string? TrackingNumber { get; set; }

        public string? TrackingUrl { get; set; }

        public IEnumerable<GetOrderLine> Lines { get; set; } = Array.Empty<GetOrderLine>();
    }
}
