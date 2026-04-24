namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Admin.Inventory;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class AdminInventoryService : IAdminInventoryService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public AdminInventoryService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<QueryResult<PagedResult<AdminInventoryItem>>> GetAsync(AdminInventoryQuery query)
        {
            var result = await SendAsync<Unit>(BuildRoute(query), Constant.ApiCallType.Get);
            return await _apiCallHelper.GetQueryResult<PagedResult<AdminInventoryItem>>(result, "We couldn't load inventory right now. Please try again.");
        }

        public async Task<ServiceResponse<AdminInventoryItem>> UpdateProductStockAsync(Guid productId, UpdateProductStock request)
        {
            var result = await SendAsync($"{Constant.AdminInventory.Product}/{productId}", Constant.ApiCallType.Update, request);
            return await _apiCallHelper.GetMutationResponse<AdminInventoryItem>(result, "We couldn't update product stock right now. Please try again.");
        }

        public async Task<ServiceResponse<AdminInventoryVariant>> UpdateVariantStockAsync(Guid variantId, UpdateVariantStock request)
        {
            var result = await SendAsync($"{Constant.AdminInventory.Variant}/{variantId}", Constant.ApiCallType.Update, request);
            return await _apiCallHelper.GetMutationResponse<AdminInventoryVariant>(result, "We couldn't update variant stock right now. Please try again.");
        }

        private async Task<HttpResponseMessage> SendAsync<TModel>(string route, string type, TModel? model = default)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            return await _apiCallHelper.ApiCallTypeCall<TModel>(new ApiCall
            {
                Client = client,
                Route = route,
                Type = type,
                Model = model,
            });
        }

        private static string BuildRoute(AdminInventoryQuery query)
        {
            var parameters = new List<string>
            {
                $"pageNumber={Math.Max(1, query.PageNumber)}",
                $"pageSize={Math.Clamp(query.PageSize, 1, 100)}",
                $"lowStockThreshold={Math.Max(0, query.LowStockThreshold)}",
                $"lowStockOnly={query.LowStockOnly.ToString().ToLowerInvariant()}",
                $"outOfStockOnly={query.OutOfStockOnly.ToString().ToLowerInvariant()}",
            };

            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                parameters.Add($"searchTerm={Uri.EscapeDataString(query.SearchTerm.Trim())}");
            }

            return $"{Constant.AdminInventory.Base}?{string.Join("&", parameters)}";
        }
    }
}
