namespace BlazorShop.Domain.Entities.Payment
{
    public class Order
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string UserId { get; set; } = string.Empty;

        public string Status { get; set; } = "Pending";

        public string Reference { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();

        public string? ShippingCarrier { get; set; }

        public string? TrackingNumber { get; set; }

        public string? TrackingUrl { get; set; }

        public string ShippingStatus { get; set; } = "PendingShipment";

        public DateTime? ShippedOn { get; set; }

        public DateTime? DeliveredOn { get; set; }

        public DateTime? LastTrackingUpdate { get; set; }
    }
}
