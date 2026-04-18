namespace BlazorShop.Domain.Contracts
{
    using BlazorShop.Domain.Entities;

    public interface IProductReadRepository
    {
        Task<IEnumerable<Product>> GetCatalogProductsAsync();

        Task<PagedResult<CatalogProductReadModel>> GetCatalogPageAsync(ProductCatalogQuery query);

        Task<PagedResult<CatalogProductReadModel>> GetPublishedCatalogPageAsync(ProductCatalogQuery query);

        Task<IReadOnlyList<PublishedProductSitemapEntryReadModel>> GetPublishedProductSitemapEntriesAsync();

        Task<Product?> GetProductDetailsByIdAsync(Guid id);

        Task<Product?> GetPublishedProductBySlugAsync(string slug);

        Task<IReadOnlyList<CatalogProductReadModel>> GetPublishedProductsByCategoryAsync(Guid categoryId);

        Task<bool> ProductSlugExistsAsync(string slug, Guid? excludedProductId = null);

        Task<IReadOnlyDictionary<Guid, Product>> GetProductsByIdsAsync(IEnumerable<Guid> productIds);
    }
}