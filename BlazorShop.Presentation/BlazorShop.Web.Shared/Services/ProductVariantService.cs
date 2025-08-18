namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class ProductVariantService : IProductVariantService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public ProductVariantService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<IEnumerable<GetProductVariant>> GetByProductIdAsync(Guid productId)
        {
            var client = _httpClientHelper.GetPublicClient();
            var currentApiCall = new ApiCall
            {
                Route = $"{Constant.Product.Variants}/{productId}/variants",
                Type = Constant.ApiCallType.Get,
                Client = client
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return result.IsSuccessStatusCode
                ? await _apiCallHelper.GetServiceResponse<IEnumerable<GetProductVariant>>(result)
                : Array.Empty<GetProductVariant>();
        }

        public async Task<ServiceResponse> AddAsync(Guid productId, CreateOrUpdateProductVariant variant)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            variant.ProductId = productId;
            var currentApiCall = new ApiCall
            {
                Route = $"{Constant.Product.Variants}/{productId}/variants",
                Type = Constant.ApiCallType.Post,
                Client = client,
                Model = variant
            };

            var result = await _apiCallHelper.ApiCallTypeCall<CreateOrUpdateProductVariant>(currentApiCall);
            return result is null || !result.IsSuccessStatusCode
                ? _apiCallHelper.ConnectionError()
                : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }

        public async Task<ServiceResponse> UpdateAsync(CreateOrUpdateProductVariant variant)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = $"{Constant.Product.Variants}/variants",
                Type = Constant.ApiCallType.Update,
                Client = client,
                Model = variant
            };

            var result = await _apiCallHelper.ApiCallTypeCall<CreateOrUpdateProductVariant>(currentApiCall);
            return result is null || !result.IsSuccessStatusCode
                ? _apiCallHelper.ConnectionError()
                : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }

        public async Task<ServiceResponse> DeleteAsync(Guid variantId)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = $"{Constant.Product.Variants}/variants/{variantId}",
                Type = Constant.ApiCallType.Delete,
                Client = client
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return result is null || !result.IsSuccessStatusCode
                ? _apiCallHelper.ConnectionError()
                : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }
    }
}
