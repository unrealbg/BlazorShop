namespace BlazorShop.Application.DTOs.Admin.Inventory
{
    public class AdminInventoryItemDto
    {
        public Guid ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public string? CategoryName { get; set; }

        public int Quantity { get; set; }

        public int VariantStock { get; set; }

        public bool IsLowStock { get; set; }

        public bool IsOutOfStock { get; set; }

        public IReadOnlyList<AdminInventoryVariantDto> Variants { get; set; } = Array.Empty<AdminInventoryVariantDto>();
    }
}
