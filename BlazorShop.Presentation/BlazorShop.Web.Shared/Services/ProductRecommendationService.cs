namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class ProductRecommendationService : IProductRecommendationService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public ProductRecommendationService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<IEnumerable<GetProductRecommendation>> GetRecommendationsAsync(Guid productId)
        {
            var client = _httpClientHelper.GetPublicClient();
            var currentApiCall = new ApiCall
            {
                Route = $"{Constant.ProductRecommendation.Get}/{productId}",
                Type = Constant.ApiCallType.Get,
                Client = client,
                Model = null!
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);

            return result.IsSuccessStatusCode
                 ? await _apiCallHelper.GetServiceResponse<IEnumerable<GetProductRecommendation>>(result)
              : [];
        }
    }
}
