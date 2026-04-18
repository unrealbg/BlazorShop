namespace BlazorShop.Application.DTOs.Discovery
{
    public sealed class GetPublicCatalogSitemap
    {
        public IReadOnlyList<GetCategorySitemapEntry> Categories { get; set; } = Array.Empty<GetCategorySitemapEntry>();

        public IReadOnlyList<GetProductSitemapEntry> Products { get; set; } = Array.Empty<GetProductSitemapEntry>();
    }
}