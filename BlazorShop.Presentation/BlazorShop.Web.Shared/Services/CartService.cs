namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Services.Contracts;

    using System.Net;
    using System.Net.Http.Json;

    public class CartService : ICartService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;
        private readonly ICheckoutAttemptStore _checkoutAttemptStore;

        public CartService(
            IHttpClientHelper httpClientHelper,
            IApiCallHelper apiCallHelper,
            ICheckoutAttemptStore checkoutAttemptStore)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
            _checkoutAttemptStore = checkoutAttemptStore;
        }

        public async Task<ServiceResponse<CheckoutResult>> Checkout(Checkout checkout)
        {
            var privateClient = await _httpClientHelper.GetPrivateClientAsync();
            var attempt = await _checkoutAttemptStore.GetOrCreateAsync(checkout);
            using var request = new HttpRequestMessage(HttpMethod.Post, Constant.Cart.Checkout)
            {
                Content = JsonContent.Create(checkout),
            };
            request.Headers.Add("Idempotency-Key", attempt.IdempotencyKey.ToString("D"));
            using var result = await privateClient.SendAsync(request);
            var response = await _apiCallHelper.GetMutationResponse<CheckoutResult>(
                result,
                "Checkout could not be completed. Please try again.");
            if (result.StatusCode == HttpStatusCode.BadRequest
                && response.ResponseType == ServiceResponseType.ValidationError)
            {
                await _checkoutAttemptStore.ClearAsync(attempt.IdempotencyKey);
            }

            return response;
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

            return result is null || !result.IsSuccessStatusCode
                       ? _apiCallHelper.ConnectionError()
                       : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }

        public async Task<QueryResult<IEnumerable<GetOrderItem>>> GetOrderItemsAsync()
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
            return await _apiCallHelper.GetQueryResult<IEnumerable<GetOrderItem>>(
                result,
                "We couldn't load order items right now. Please try again.");
        }

        public async Task<QueryResult<IEnumerable<GetOrderItem>>> GetCheckoutHistoryByUserId()
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Cart.GetUserOrderItems,
                Type = Constant.ApiCallType.Get,
                Client = client,
                Model = null!,
                Id = null!
            };
            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return await _apiCallHelper.GetQueryResult<IEnumerable<GetOrderItem>>(
                result,
                "We couldn't load your order history right now. Please try again.");
        }
    }
}
