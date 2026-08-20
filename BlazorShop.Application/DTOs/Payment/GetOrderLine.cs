namespace BlazorShop.Application.DTOs.Payment
{
    public class GetOrderLine
    {
        public Guid ProductId { get; set; }

        public Guid? VariantId { get; set; }

        public string? ProductName { get; set; }

        public string? Sku { get; set; }

        public string? SizeScale { get; set; }

        public string? SizeValue { get; set; }

        public string? Color { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal LineTotal { get; set; }
    }
}
