namespace BlazorShop.Application.DTOs.Admin.Orders
{
    using BlazorShop.Domain.Entities.Payment;

    public class AdminOrderQueryDto
    {
        public string? SearchTerm { get; set; }

        public OrderStatus? OrderStatus { get; set; }

        public OrderPaymentStatus? PaymentStatus { get; set; }

        public FulfillmentStatus? FulfillmentStatus { get; set; }

        public DateTime? FromUtc { get; set; }

        public DateTime? ToUtc { get; set; }

        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 25;
    }
}
