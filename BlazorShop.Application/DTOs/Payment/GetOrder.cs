namespace BlazorShop.Application.DTOs.Payment
{
    public class GetOrder
    {
        public Guid Id { get; set; }

        public string Reference { get; set; } = string.Empty;

        public string OrderStatus { get; set; } = string.Empty;

        public string PaymentStatus { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = string.Empty;

        public string FulfillmentStatus { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public decimal SubtotalAmount { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal ShippingAmount { get; set; }

        public decimal TaxAmount { get; set; }

        public string Currency { get; set; } = string.Empty;

        public DateTime CreatedOn { get; set; }

        public string? ShippingCarrier { get; set; }

        public string? TrackingNumber { get; set; }

        public string? TrackingUrl { get; set; }

        public DateTime? ShippedOn { get; set; }

        public DateTime? DeliveredOn { get; set; }

        public string? UserId { get; set; }

        public string? CustomerName { get; set; }

        public string? CustomerEmail { get; set; }

        public string? ShippingAddress { get; set; }

        public string? BillingAddress { get; set; }

        public string? AdminNote { get; set; }

        public uint Version { get; set; }

        public IEnumerable<GetOrderLine> Lines { get; set; } = Array.Empty<GetOrderLine>();
    }
}
