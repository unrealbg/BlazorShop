namespace BlazorShop.Web.Shared.Models.Product
{
    public class GetProductVariant
    {
        public Guid Id { get; set; }

        public Guid ProductId { get; set; }

        public string? Sku { get; set; }

        public int SizeScale { get; set; }

        public string SizeValue { get; set; } = string.Empty;

        public decimal? Price { get; set; }

        public int Stock { get; set; }

        public string? Color { get; set; }

        public bool IsDefault { get; set; }
    }
}