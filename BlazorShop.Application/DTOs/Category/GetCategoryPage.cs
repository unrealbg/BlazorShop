namespace BlazorShop.Application.DTOs.Category
{
    using BlazorShop.Application.DTOs.Product;

    public sealed class GetCategoryPage
    {
        public GetCategory Category { get; set; } = new();

        public IReadOnlyList<GetCatalogProduct> Products { get; set; } = Array.Empty<GetCatalogProduct>();
    }
}