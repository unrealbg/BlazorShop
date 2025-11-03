namespace BlazorShop.Domain.Contracts
{
    using BlazorShop.Domain.Entities;

    public interface IProductRecommendationRepository
    {
        Task<IEnumerable<Product>> GetRelatedProductsByCategoryAsync(Guid productId, Guid categoryId, int count = 4);

        Task<IEnumerable<Product>> GetFrequentlyBoughtTogetherAsync(Guid productId, int count = 4);

        Task<IEnumerable<Product>> GetRecentlyViewedProductsAsync(IEnumerable<Guid> productIds, int count = 4);
    }
}
