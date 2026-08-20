namespace BlazorShop.Web.Shared.Models.Payment
{
    public class GetOrderLine
    {
        public Guid ProductId { get; set; }

        public Guid? VariantId { get; set; }

        public string? ProductName { get; set; }

        public string? Sku { get; set; }

        public string? SizeValue { get; set; }

        public string? Color { get; set; }

        public int Quantity { get; set; }

        public decimal UnitPrice { get; set; }

        public decimal LineTotal => UnitPrice * Quantity;

        public string? VariantLabel
        {
            get
            {
                var details = new[]
                    {
                        string.IsNullOrWhiteSpace(Sku) ? null : $"SKU {Sku}",
                        string.IsNullOrWhiteSpace(SizeValue) ? null : $"Size {SizeValue}",
                        string.IsNullOrWhiteSpace(Color) ? null : Color,
                    }
                    .Where(value => value is not null)
                    .ToArray();

                return details.Length == 0 ? null : string.Join(" · ", details);
            }
        }
    }
}
