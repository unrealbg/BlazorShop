namespace BlazorShop.Application.DTOs.Admin.Orders
{
    public class AdminOrderQueryDto
    {
        public string? SearchTerm { get; set; }

        public string? Status { get; set; }

        public string? ShippingStatus { get; set; }

        public DateTime? FromUtc { get; set; }

        public DateTime? ToUtc { get; set; }

        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 25;
    }
}
