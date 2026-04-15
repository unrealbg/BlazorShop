namespace BlazorShop.Domain.Contracts
{
    using BlazorShop.Domain.Entities;

    public interface IProductReadRepository
    {
        Task<IEnumerable<Product>> GetCatalogProductsAsync();

        Task<Product?> GetProductDetailsByIdAsync(Guid id);

        Task<IReadOnlyDictionary<Guid, Product>> GetProductsByIdsAsync(IEnumerable<Guid> productIds);
    }
}