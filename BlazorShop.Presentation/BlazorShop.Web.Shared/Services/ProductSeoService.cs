namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Seo;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class ProductSeoService : IProductSeoService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public ProductSeoService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<QueryResult<GetProductSeo>> GetByProductIdAsync(Guid productId)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = $"{Constant.Seo.Products}/{productId}/seo",
                Type = Constant.ApiCallType.Get,
                Client = client,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return await _apiCallHelper.GetQueryResult<GetProductSeo>(
                result,
                "We couldn't load product SEO right now. Please try again.");
        }

        public async Task<ServiceResponse<GetProductSeo>> UpdateAsync(Guid productId, UpdateProductSeo request)
        {
            request.ProductId = productId;

            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = $"{Constant.Seo.Products}/{productId}/seo",
                Type = Constant.ApiCallType.Update,
                Client = client,
                Model = request,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<UpdateProductSeo>(currentApiCall);
            return await _apiCallHelper.GetMutationResponse<GetProductSeo>(
                result,
                "We couldn't save product SEO right now. Please try again.");
        }
    }
}