namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Seo;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class SeoSettingsService : ISeoSettingsService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public SeoSettingsService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<QueryResult<GetSeoSettings>> GetAsync()
        {
            var client = _httpClientHelper.GetPublicClient();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Seo.Settings,
                Type = Constant.ApiCallType.Get,
                Client = client,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return await _apiCallHelper.GetQueryResult<GetSeoSettings>(
                result,
                "We couldn't load SEO settings right now. Please try again.");
        }

        public async Task<ServiceResponse<GetSeoSettings>> UpdateAsync(UpdateSeoSettings request)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Seo.AdminSettings,
                Type = Constant.ApiCallType.Update,
                Client = client,
                Model = request,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<UpdateSeoSettings>(currentApiCall);
            return await _apiCallHelper.GetMutationResponse<GetSeoSettings>(
                result,
                "We couldn't save SEO settings right now. Please try again.");
        }
    }
}