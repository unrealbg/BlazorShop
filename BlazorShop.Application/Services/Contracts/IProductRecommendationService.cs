namespace BlazorShop.Application.Services.Contracts
{
    using BlazorShop.Application.DTOs.Product;

    public interface IProductRecommendationService
    {
        Task<IEnumerable<GetProductRecommendation>> GetRecommendationsForProductAsync(Guid productId);
    }
}
