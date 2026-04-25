namespace BlazorShop.Web.Shared.Services
{
    using System.Globalization;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class ProductService : IProductService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public ProductService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<QueryResult<IEnumerable<GetProduct>>> GetAllAsync()
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var currentApiCall = new ApiCall
            {
                Route = Constant.Product.GetAll,
                Type = Constant.ApiCallType.Get,
                Client = client,
                Model = null!,
                Id = null!
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return await _apiCallHelper.GetQueryResult<IEnumerable<GetProduct>>(
                result,
                "We couldn't load products right now. Please try again.");
        }

        public async Task<QueryResult<PagedResult<GetCatalogProduct>>> GetCatalogPageAsync(ProductCatalogQuery query)
        {
            var client = _httpClientHelper.GetPublicClient();
            var currentApiCall = new ApiCall
            {
                Route = BuildCatalogRoute(query),
                Type = Constant.ApiCallType.Get,
                Client = client,
            };

            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(currentApiCall);
            return await _apiCallHelper.GetQueryResult<PagedResult<GetCatalogProduct>>(
                result,
                "We couldn't load the product catalog right now. Please try again.");
        }

        public async Task<QueryResult<GetProduct>> GetByIdAsync(Guid id)
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
            return await _apiCallHelper.GetQueryResult<GetProduct>(
                result,
                "We couldn't load this product right now. Please try again.");
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

            return result is null || !result.IsSuccessStatusCode
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

            return result is null || !result.IsSuccessStatusCode
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

            return result is null || !result.IsSuccessStatusCode
                       ? _apiCallHelper.ConnectionError()
                       : await _apiCallHelper.GetServiceResponse<ServiceResponse>(result);
        }

        private static string BuildCatalogRoute(ProductCatalogQuery query)
        {
            var parameters = new List<string>
            {
                $"pageNumber={Math.Max(1, query.PageNumber)}",
                $"pageSize={Math.Max(1, query.PageSize)}",
                $"sortBy={Uri.EscapeDataString(query.SortBy.ToString())}",
            };

            if (query.CategoryId.HasValue && query.CategoryId.Value != Guid.Empty)
            {
                parameters.Add($"categoryId={query.CategoryId.Value}");
            }

            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                parameters.Add($"searchTerm={Uri.EscapeDataString(query.SearchTerm.Trim())}");
            }

            if (query.CreatedAfterUtc.HasValue)
            {
                parameters.Add($"createdAfterUtc={Uri.EscapeDataString(query.CreatedAfterUtc.Value.ToString("O", CultureInfo.InvariantCulture))}");
            }

            return $"{Constant.Product.GetCatalog}?{string.Join("&", parameters)}";
        }
    }
}
