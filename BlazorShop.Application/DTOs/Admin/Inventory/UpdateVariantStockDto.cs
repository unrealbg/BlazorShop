namespace BlazorShop.Application.DTOs.Admin.Inventory
{
    public class UpdateVariantStockDto
    {
        public int Stock { get; set; }

        public int? ExpectedStock { get; set; }
    }
}
