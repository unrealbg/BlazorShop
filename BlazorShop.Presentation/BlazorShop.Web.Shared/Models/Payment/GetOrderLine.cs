namespace BlazorShop.Web.Shared.Models.Payment
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

        public string? VariantLabel
        {
            get
            {
                var details = new[]
                    {
                        string.IsNullOrWhiteSpace(Sku) ? null : $"SKU {Sku}",
                        GetSizeLabel(),
                        string.IsNullOrWhiteSpace(Color) ? null : Color,
                    }
                    .Where(value => value is not null)
                    .ToArray();

                return details.Length == 0 ? null : string.Join(" · ", details);
            }
        }

        private string? GetSizeLabel()
        {
            if (string.IsNullOrWhiteSpace(SizeValue))
            {
                return null;
            }

            return SizeScale switch
            {
                "ClothingAlpha" => $"Clothing {SizeValue}",
                "ClothingNumericEU" or "ShoesEU" => $"EU {SizeValue}",
                "ShoesUS" => $"US {SizeValue}",
                "ShoesUK" => $"UK {SizeValue}",
                null or "" or "Unknown" => $"Size {SizeValue}",
                _ => $"{SizeScale} {SizeValue}",
            };
        }
    }
}
