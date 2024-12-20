namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class CartService : ICartService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public CartService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<ServiceResponse> Checkout(Checkout checkout)
        {
            var privateClient = await _httpClientHelper.GetPrivateClientAsync();
            var apiCallModel = new ApiCall
            {
                Route = Constant.Cart.Checkout,
                Type = Constant.ApiCallType.Post,
                Client = privateClient,
                Id = null!,
                Model = checkout
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Checkout>(apiCallModel);
            return result == null
                       ? _apiCallHelper.ConnectionError()
                       : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }

        public async Task<ServiceResponse> SaveCheckoutHistory(IEnumerable<CreateAchieve> achieves)
        {
            var privateClient = await _httpClientHelper.GetPrivateClientAsync();
            var apiCallModel = new ApiCall
                                   {
                                       Route = Constant.Cart.SaveCart,
                                       Type = Constant.ApiCallType.Post,
                                       Client = privateClient,
                                       Id = null!,
                                       Model = achieves
            };

            var result = await _apiCallHelper.ApiCallTypeCall<IEnumerable<CreateAchieve>>(apiCallModel);
            return result == null
                       ? _apiCallHelper.ConnectionError()
                       : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }
    }
}
