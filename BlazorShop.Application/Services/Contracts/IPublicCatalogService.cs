namespace BlazorShop.Application.Services.Contracts
{
    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.DTOs.Discovery;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Domain.Contracts;

    public interface IPublicCatalogService
    {
        Task<IEnumerable<GetCategory>> GetPublishedCategoriesAsync();

        Task<GetPublicCatalogSitemap> GetPublishedSitemapAsync();

        Task<PagedResult<GetCatalogProduct>> GetPublishedCatalogPageAsync(ProductCatalogQuery query);

        Task<GetProduct?> GetPublishedProductBySlugAsync(string slug);

        Task<GetCategoryPage?> GetPublishedCategoryPageBySlugAsync(string slug);
    }
}