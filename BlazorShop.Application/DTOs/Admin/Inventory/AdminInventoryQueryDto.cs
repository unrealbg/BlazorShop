namespace BlazorShop.Application.DTOs.Admin.Inventory
{
    public class AdminInventoryQueryDto
    {
        public string? SearchTerm { get; set; }

        public bool LowStockOnly { get; set; }

        public bool OutOfStockOnly { get; set; }

        public int LowStockThreshold { get; set; } = 5;

        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}
