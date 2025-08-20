namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Newsletter;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class NewsletterService : INewsletterService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public NewsletterService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<ServiceResponse> SubscribeAsync(SubscribeRequest request)
        {
            var client = _httpClientHelper.GetPublicClient();
            var currentApiCall = new ApiCall
                                     {
                                         Route = Constant.Newsletter.Subscribe,
                                         Type = Constant.ApiCallType.Post,
                                         Client = client,
                                         Id = null!,
                                         Model = request,
                                     };

            var result = await _apiCallHelper.ApiCallTypeCall<SubscribeRequest>(currentApiCall);

            return result is null || !result.IsSuccessStatusCode
                       ? _apiCallHelper.ConnectionError()
                       : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }
    }
}
