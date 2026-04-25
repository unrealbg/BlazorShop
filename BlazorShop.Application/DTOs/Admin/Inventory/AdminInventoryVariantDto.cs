namespace BlazorShop.Application.DTOs.Admin.Inventory
{
    public class AdminInventoryVariantDto
    {
        public Guid VariantId { get; set; }

        public Guid ProductId { get; set; }

        public string? ProductName { get; set; }

        public string? Sku { get; set; }

        public string SizeScale { get; set; } = string.Empty;

        public string SizeValue { get; set; } = string.Empty;

        public string? Color { get; set; }

        public int Stock { get; set; }

        public bool IsLowStock { get; set; }

        public bool IsOutOfStock { get; set; }
    }
}
