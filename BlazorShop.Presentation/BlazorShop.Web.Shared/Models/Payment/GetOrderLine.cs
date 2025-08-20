namespace BlazorShop.Web.Shared.Models.Payment
{
    public class GetOrderLine
    {
        public Guid ProductId { get; set; }

        public string? ProductName { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal LineTotal => UnitPrice * Quantity;
    }
}
