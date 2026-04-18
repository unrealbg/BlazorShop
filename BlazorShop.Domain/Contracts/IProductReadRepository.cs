namespace BlazorShop.Domain.Contracts
{
    using BlazorShop.Domain.Entities;

    public interface IProductReadRepository
    {
        Task<IEnumerable<Product>> GetCatalogProductsAsync();

        Task<PagedResult<CatalogProductReadModel>> GetCatalogPageAsync(ProductCatalogQuery query);

        Task<Product?> GetProductDetailsByIdAsync(Guid id);

        Task<bool> ProductSlugExistsAsync(string slug, Guid? excludedProductId = null);

        Task<IReadOnlyDictionary<Guid, Product>> GetProductsByIdsAsync(IEnumerable<Guid> productIds);
    }
}