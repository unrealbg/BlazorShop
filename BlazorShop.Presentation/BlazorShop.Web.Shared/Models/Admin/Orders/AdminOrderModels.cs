namespace BlazorShop.Web.Shared.Models.Admin.Orders
{
    public class AdminOrderQuery
    {
        public string? SearchTerm { get; set; }

        public string? Status { get; set; }

        public string? ShippingStatus { get; set; }

        public DateTime? FromUtc { get; set; }

        public DateTime? ToUtc { get; set; }

        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 25;
    }

    public class UpdateOrderAdminNote
    {
        public string? AdminNote { get; set; }
    }
}
