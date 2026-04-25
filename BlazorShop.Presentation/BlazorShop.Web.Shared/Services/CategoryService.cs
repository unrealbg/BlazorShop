namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class CategoryService : ICategoryService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public CategoryService(IHttpClientHelper client, IApiCallHelper apiCall)
        {
            _httpClientHelper = client;
            _apiCallHelper = apiCall;
        }

        public async Task<QueryResult<IEnumerable<GetCategory>>> GetAllAsync()
        {
            var client = _httpClientHelper.GetPublicClient();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Category.GetAll,
                Type = Constant.ApiCallType.Get,
                Client = client,
                Model = null!,
                Id = null!
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return await _apiCallHelper.GetQueryResult<IEnumerable<GetCategory>>(
                result,
                "We couldn't load categories right now. Please try again.");
        }

        public async Task<QueryResult<IEnumerable<GetCategory>>> GetAllForAdminAsync()
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Category.GetAllForAdmin,
                Type = Constant.ApiCallType.Get,
                Client = client,
                Model = null!,
                Id = null!
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return await _apiCallHelper.GetQueryResult<IEnumerable<GetCategory>>(
                result,
                "We couldn't load categories right now. Please try again.");
        }

        public async Task<QueryResult<GetCategory>> GetByIdAsync(Guid id)
        {
            var client = _httpClientHelper.GetPublicClient();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Category.Get,
                Type = Constant.ApiCallType.Get,
                Client = client,
                Model = null!,
            };

            currentApiCall.ToString(id);
            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return await _apiCallHelper.GetQueryResult<GetCategory>(
                result,
                "We couldn't load this category right now. Please try again.");
        }

        public async Task<ServiceResponse> AddAsync(CreateCategory category)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Category.Add,
                Type = Constant.ApiCallType.Post,
                Client = client,
                Id = null!,
                Model = category,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<CreateCategory>(currentApiCall);

            return result is null || !result.IsSuccessStatusCode
                       ? _apiCallHelper.ConnectionError()
                       : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }

        public async Task<ServiceResponse> UpdateAsync(UpdateCategory category)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Category.Update,
                Type = Constant.ApiCallType.Update,
                Client = client,
                Id = null!,
                Model = category,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<UpdateCategory>(currentApiCall);

            return result is null || !result.IsSuccessStatusCode
                       ? _apiCallHelper.ConnectionError()
                       : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }

        public async Task<ServiceResponse> DeleteAsync(Guid id)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = $"{Constant.Category.Delete}/{id}",
                Type = Constant.ApiCallType.Delete,
                Client = client,
                Model = null!,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);

            return result is null || !result.IsSuccessStatusCode
                       ? _apiCallHelper.ConnectionError()
                       : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }

        public async Task<QueryResult<IEnumerable<GetProduct>>> GetProductsByCategoryAsync(Guid id)
        {
            var client = _httpClientHelper.GetPublicClient();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Category.GetProductByCategory,
                Type = Constant.ApiCallType.Get,
                Client = client,
                Model = null!,
            };

            currentApiCall.ToString(id);
            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return await _apiCallHelper.GetQueryResult<IEnumerable<GetProduct>>(
                result,
                "We couldn't load products for this category right now. Please try again.");
        }
    }
}