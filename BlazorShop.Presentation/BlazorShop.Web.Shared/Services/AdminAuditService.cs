namespace BlazorShop.Web.Shared.Services
{
    using System.Globalization;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Admin.Audit;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class AdminAuditService : IAdminAuditService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public AdminAuditService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<QueryResult<PagedResult<AdminAuditLog>>> GetAsync(AdminAuditQuery query)
        {
            var result = await SendAsync<Unit>(BuildRoute(query), Constant.ApiCallType.Get);
            return await _apiCallHelper.GetQueryResult<PagedResult<AdminAuditLog>>(result, "We couldn't load the audit log right now. Please try again.");
        }

        public async Task<QueryResult<AdminAuditLog>> GetByIdAsync(Guid id)
        {
            var result = await SendAsync<Unit>($"{Constant.AdminAudit.Base}/{id}", Constant.ApiCallType.Get);
            return await _apiCallHelper.GetQueryResult<AdminAuditLog>(result, "We couldn't load this audit entry right now. Please try again.");
        }

        private async Task<HttpResponseMessage> SendAsync<TModel>(string route, string type)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            return await _apiCallHelper.ApiCallTypeCall<TModel>(new ApiCall
            {
                Client = client,
                Route = route,
                Type = type,
            });
        }

        private static string BuildRoute(AdminAuditQuery query)
        {
            var parameters = new List<string>
            {
                $"pageNumber={Math.Max(1, query.PageNumber)}",
                $"pageSize={Math.Clamp(query.PageSize, 1, 100)}",
            };

            Add(parameters, "actor", query.Actor);
            Add(parameters, "action", query.Action);
            Add(parameters, "entityType", query.EntityType);
            Add(parameters, "entityId", query.EntityId);

            if (query.FromUtc.HasValue)
            {
                parameters.Add($"fromUtc={Uri.EscapeDataString(query.FromUtc.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}");
            }

            if (query.ToUtc.HasValue)
            {
                parameters.Add($"toUtc={Uri.EscapeDataString(query.ToUtc.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}");
            }

            return $"{Constant.AdminAudit.Base}?{string.Join("&", parameters)}";
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
