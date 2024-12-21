namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Services.Contracts;
    using System.Net;

    public class ProductService : IProductService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public ProductService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<IEnumerable<GetProduct>> GetAllAsync()
        {
            var client = _httpClientHelper.GetPublicClient();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Product.GetAll,
                Type = Constant.ApiCallType.Get,
                Client = client,
                Model = null!,
                Id = null!
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);

            return result.IsSuccessStatusCode
                       ? await _apiCallHelper.GetServiceResponse<IEnumerable<GetProduct>>(result)
                       : [];
        }

        public async Task<GetProduct> GetByIdAsync(Guid id)
        {
            var client = _httpClientHelper.GetPublicClient();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Product.Get,
                Type = Constant.ApiCallType.Get,
                Client = client,
                Model = null!,
            };

            currentApiCall.ToString(id);
            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);

            return result.IsSuccessStatusCode
                       ? await _apiCallHelper.GetServiceResponse<GetProduct>(result)
                       : null!;
        }

        public async Task<ServiceResponse> AddAsync(CreateProduct product)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Product.Add,
                Type = Constant.ApiCallType.Post,
                Client = client,
                Id = null!,
                Model = product,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<CreateProduct>(currentApiCall);
            return result == null
                       ? _apiCallHelper.ConnectionError()
                       : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }

        public async Task<ServiceResponse> UpdateAsync(UpdateProduct product)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Product.Update,
                Type = Constant.ApiCallType.Update,
                Client = client,
                Id = null!,
                Model = product,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<UpdateProduct>(currentApiCall);
            return result == null
                       ? _apiCallHelper.ConnectionError()
                       : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }

        public async Task<ServiceResponse> DeleteAsync(Guid id)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = $"{Constant.Product.Delete}/{id}",
                Type = Constant.ApiCallType.Delete,
                Client = client,
                Model = null!,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return result == null
                       ? _apiCallHelper.ConnectionError()
                       : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }
    }
}
