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

        Task<GetProduct?> GetPublishedProductByIdAsync(Guid id);

        Task<GetProduct?> GetPublishedProductBySlugAsync(string slug);

        Task<GetCategory?> GetPublishedCategoryByIdAsync(Guid id);

        Task<IReadOnlyList<GetCatalogProduct>> GetPublishedProductsByCategoryAsync(Guid categoryId);

        Task<GetCategoryPage?> GetPublishedCategoryPageBySlugAsync(string slug);
    }
}