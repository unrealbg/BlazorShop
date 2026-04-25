namespace BlazorShop.Web.Shared.Models.Admin.Inventory
{
    public class AdminInventoryQuery
    {
        public string? SearchTerm { get; set; }

        public bool LowStockOnly { get; set; }

        public bool OutOfStockOnly { get; set; }

        public int LowStockThreshold { get; set; } = 5;

        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }

    public class AdminInventoryItem
    {
        public Guid ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string? CategoryName { get; set; }

        public int Quantity { get; set; }

        public int VariantStock { get; set; }

        public bool IsLowStock { get; set; }

        public bool IsOutOfStock { get; set; }

        public IReadOnlyList<AdminInventoryVariant> Variants { get; set; } = Array.Empty<AdminInventoryVariant>();
    }

    public class AdminInventoryVariant
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

    public class UpdateProductStock
    {
        public int Quantity { get; set; }
    }

    public class UpdateVariantStock
    {
        public int Stock { get; set; }
    }
}
