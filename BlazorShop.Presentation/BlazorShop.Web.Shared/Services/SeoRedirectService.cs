namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Seo;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class SeoRedirectService : ISeoRedirectService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public SeoRedirectService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<QueryResult<IReadOnlyList<GetSeoRedirect>>> GetAllAsync()
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Seo.Redirects,
                Type = Constant.ApiCallType.Get,
                Client = client,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return await _apiCallHelper.GetQueryResult<IReadOnlyList<GetSeoRedirect>>(
                result,
                "We couldn't load SEO redirects right now. Please try again.");
        }

        public async Task<QueryResult<GetSeoRedirect>> GetByIdAsync(Guid redirectId)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Seo.Redirects,
                Type = Constant.ApiCallType.Get,
                Client = client,
                Id = redirectId.ToString(),
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return await _apiCallHelper.GetQueryResult<GetSeoRedirect>(
                result,
                "We couldn't load this SEO redirect right now. Please try again.");
        }

        public async Task<ServiceResponse<GetSeoRedirect>> CreateAsync(UpsertSeoRedirect request)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Seo.Redirects,
                Type = Constant.ApiCallType.Post,
                Client = client,
                Model = request,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<UpsertSeoRedirect>(currentApiCall);
            return await _apiCallHelper.GetMutationResponse<GetSeoRedirect>(
                result,
                "We couldn't create the SEO redirect right now. Please try again.");
        }

        public async Task<ServiceResponse<GetSeoRedirect>> UpdateAsync(Guid redirectId, UpsertSeoRedirect request)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = $"{Constant.Seo.Redirects}/{redirectId}",
                Type = Constant.ApiCallType.Update,
                Client = client,
                Model = request,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<UpsertSeoRedirect>(currentApiCall);
            return await _apiCallHelper.GetMutationResponse<GetSeoRedirect>(
                result,
                "We couldn't update the SEO redirect right now. Please try again.");
        }

        public async Task<ServiceResponse<GetSeoRedirect>> DeactivateAsync(Guid redirectId)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = $"{Constant.Seo.Redirects}/{redirectId}/deactivate",
                Type = Constant.ApiCallType.Post,
                Client = client,
                Model = null!,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return await _apiCallHelper.GetMutationResponse<GetSeoRedirect>(
                result,
                "We couldn't deactivate the SEO redirect right now. Please try again.");
        }

        public async Task<ServiceResponse<GetSeoRedirect>> DeleteAsync(Guid redirectId)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Seo.Redirects,
                Type = Constant.ApiCallType.Delete,
                Client = client,
                Id = redirectId.ToString(),
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return await _apiCallHelper.GetMutationResponse<GetSeoRedirect>(
                result,
                "We couldn't delete the SEO redirect right now. Please try again.");
        }
    }
}