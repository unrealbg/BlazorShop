namespace BlazorShop.Domain.Entities.Payment
{
    public class Order
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string UserId { get; set; } = string.Empty;

        public OrderStatus OrderStatus { get; set; } = OrderStatus.Pending;

        public OrderPaymentStatus PaymentStatus { get; set; } = OrderPaymentStatus.Pending;

        public OrderPaymentMethod PaymentMethod { get; set; } = OrderPaymentMethod.Unknown;

        public FulfillmentStatus FulfillmentStatus { get; set; } = FulfillmentStatus.NotStarted;

        public string? LegacyStatus { get; private set; }

        public string? LegacyShippingStatus { get; private set; }

        public string Reference { get; set; } = string.Empty;

        public decimal TotalAmount { get; set; }

        public decimal SubtotalAmount { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal ShippingAmount { get; set; }

        public decimal TaxAmount { get; set; }

        public string Currency { get; set; } = string.Empty;

        public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

        public string? CustomerNameSnapshot { get; set; }

        public string? CustomerEmailSnapshot { get; set; }

        public string? ShippingAddressSnapshot { get; set; }

        public string? BillingAddressSnapshot { get; set; }

        public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();

        public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();

        public string? ShippingCarrier { get; set; }

        public string? TrackingNumber { get; set; }

        public string? TrackingUrl { get; set; }

        public DateTime? ShippedOn { get; set; }

        public DateTime? DeliveredOn { get; set; }

        public DateTime? LastTrackingUpdate { get; set; }

        public string? AdminNote { get; set; }

        public uint Version { get; private set; }
    }
}
