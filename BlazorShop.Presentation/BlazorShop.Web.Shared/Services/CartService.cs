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

    }
}
