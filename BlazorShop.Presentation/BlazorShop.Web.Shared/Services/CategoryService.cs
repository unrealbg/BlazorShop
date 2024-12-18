namespace BlazorShop.Web.Shared.Services
{
    using System.Net;

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

        public async Task<IEnumerable<GetCategory>> GetAllAsync()
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

            return result.IsSuccessStatusCode
                       ? await this._apiCallHelper.GetServiceResponse<IEnumerable<GetCategory>>(result)
                       : [];
        }

        public async Task<GetCategory> GetByIdAsync(Guid id)
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

            return result.IsSuccessStatusCode
                       ? await this._apiCallHelper.GetServiceResponse<GetCategory>(result)
                       : null!;
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
            return result == null
                       ? this._apiCallHelper.ConnectionError()
                       : await this._apiCallHelper.GetServiceResponse<ServiceResponse>(result);
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
            return result == null
                       ? this._apiCallHelper.ConnectionError()
                       : await this._apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }

        public async Task<ServiceResponse> DeleteAsync(Guid id)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Category.Delete,
                Type = Constant.ApiCallType.Delete,
                Client = client,
                Model = null!,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return result == null
                       ? this._apiCallHelper.ConnectionError()
                       : await this._apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }

        public async Task<IEnumerable<GetProduct>> GetProductsByCategoryAsync(Guid id)
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

            return result.IsSuccessStatusCode
                       ? await this._apiCallHelper.GetServiceResponse<IEnumerable<GetProduct>>(result)
                       : [];
        }
    }
}
