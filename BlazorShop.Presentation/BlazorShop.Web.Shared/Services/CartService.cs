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

        public async Task<ServiceResponse> SaveCheckoutHistory(IEnumerable<CreateOrderItem> orderItems)
        {
            var privateClient = await _httpClientHelper.GetPrivateClientAsync();
            var apiCallModel = new ApiCall
                                   {
                                       Route = Constant.Cart.SaveCart,
                                       Type = Constant.ApiCallType.Post,
                                       Client = privateClient,
                                       Id = null!,
                                       Model = orderItems
            };

            var result = await _apiCallHelper.ApiCallTypeCall<IEnumerable<CreateOrderItem>>(apiCallModel);
            return result == null
                       ? _apiCallHelper.ConnectionError()
                       : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }

        public async Task<IEnumerable<GetOrderItem>> GetOrderItemsAsync()
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
                                     {
                                         Route = Constant.Cart.GetOrderItems,
                                         Type = Constant.ApiCallType.Get,
                                         Client = client,
                                         Model = null!,
                                         Id = null!
                                     };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);

            return result.IsSuccessStatusCode
                       ? await this._apiCallHelper.GetServiceResponse<IEnumerable<GetOrderItem>>(result)
                       : [];
        }
    }
}
