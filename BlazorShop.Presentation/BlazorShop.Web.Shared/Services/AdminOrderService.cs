namespace BlazorShop.Web.Shared.Services
{
    using System.Globalization;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Admin.Orders;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class AdminOrderService : IAdminOrderService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public AdminOrderService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<QueryResult<PagedResult<GetOrder>>> GetAsync(AdminOrderQuery query)
        {
            var result = await SendAsync<Unit>(BuildRoute(query), Constant.ApiCallType.Get);
            return await _apiCallHelper.GetQueryResult<PagedResult<GetOrder>>(result, "We couldn't load orders right now. Please try again.");
        }

        public async Task<QueryResult<GetOrder>> GetByIdAsync(Guid id)
        {
            var result = await SendAsync<Unit>($"{Constant.AdminOrders.Base}/{id}", Constant.ApiCallType.Get);
            return await _apiCallHelper.GetQueryResult<GetOrder>(result, "We couldn't load this order right now. Please try again.");
        }

        public async Task<ServiceResponse<GetOrder>> UpdateTrackingAsync(Guid id, UpdateTrackingRequest request)
        {
            var result = await SendAsync($"{Constant.AdminOrders.Base}/{id}/tracking", Constant.ApiCallType.Update, request);
            return await _apiCallHelper.GetMutationResponse<GetOrder>(result, "We couldn't update tracking right now. Please try again.");
        }

        public async Task<ServiceResponse<GetOrder>> UpdateShippingStatusAsync(Guid id, UpdateShippingStatusRequest request)
        {
            var result = await SendAsync($"{Constant.AdminOrders.Base}/{id}/shipping-status", Constant.ApiCallType.Update, request);
            return await _apiCallHelper.GetMutationResponse<GetOrder>(result, "We couldn't update shipping status right now. Please try again.");
        }

        public async Task<ServiceResponse<GetOrder>> UpdateAdminNoteAsync(Guid id, UpdateOrderAdminNote request)
        {
            var result = await SendAsync($"{Constant.AdminOrders.Base}/{id}/admin-note", Constant.ApiCallType.Update, request);
            return await _apiCallHelper.GetMutationResponse<GetOrder>(result, "We couldn't update the admin note right now. Please try again.");
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

        private static string BuildRoute(AdminOrderQuery query)
        {
            var parameters = new List<string>
            {
                $"pageNumber={Math.Max(1, query.PageNumber)}",
                $"pageSize={Math.Clamp(query.PageSize, 1, 100)}",
            };

            Add(parameters, "searchTerm", query.SearchTerm);
            Add(parameters, "status", query.Status);
            Add(parameters, "shippingStatus", query.ShippingStatus);

            if (query.FromUtc.HasValue)
            {
                parameters.Add($"fromUtc={Uri.EscapeDataString(query.FromUtc.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}");
            }

            if (query.ToUtc.HasValue)
            {
                parameters.Add($"toUtc={Uri.EscapeDataString(query.ToUtc.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}");
            }

            return $"{Constant.AdminOrders.Base}?{string.Join("&", parameters)}";
        }

        private static void Add(List<string> parameters, string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parameters.Add($"{name}={Uri.EscapeDataString(value.Trim())}");
            }
        }
    }
}
