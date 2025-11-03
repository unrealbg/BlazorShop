namespace BlazorShop.Web.Shared.Services.Contracts
{
  using BlazorShop.Web.Shared.Models.Product;

    public interface IProductRecommendationService
    {
        Task<IEnumerable<GetProductRecommendation>> GetRecommendationsAsync(Guid productId);
    }
}
