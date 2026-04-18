namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Seo;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class CategorySeoService : ICategorySeoService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public CategorySeoService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<QueryResult<GetCategorySeo>> GetByCategoryIdAsync(Guid categoryId)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = $"{Constant.Seo.Categories}/{categoryId}/seo",
                Type = Constant.ApiCallType.Get,
                Client = client,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return await _apiCallHelper.GetQueryResult<GetCategorySeo>(
                result,
                "We couldn't load category SEO right now. Please try again.");
        }

        public async Task<ServiceResponse<GetCategorySeo>> UpdateAsync(Guid categoryId, UpdateCategorySeo request)
        {
            request.CategoryId = categoryId;

            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = $"{Constant.Seo.Categories}/{categoryId}/seo",
                Type = Constant.ApiCallType.Update,
                Client = client,
                Model = request,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<UpdateCategorySeo>(currentApiCall);
            return await _apiCallHelper.GetMutationResponse<GetCategorySeo>(
                result,
                "We couldn't save category SEO right now. Please try again.");
        }
    }
}