namespace BlazorShop.Web.Shared.Models.Category
{
    using BlazorShop.Web.Shared.Models.Product;

    public sealed class GetCategoryPage
    {
        public GetCategory Category { get; set; } = new();

        public IReadOnlyList<GetCatalogProduct> Products { get; set; } = Array.Empty<GetCatalogProduct>();
    }
}