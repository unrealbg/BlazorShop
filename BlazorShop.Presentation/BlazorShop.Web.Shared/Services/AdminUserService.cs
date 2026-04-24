namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Admin.Users;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class AdminUserService : IAdminUserService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public AdminUserService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<QueryResult<PagedResult<AdminUserListItem>>> GetUsersAsync(AdminUserQuery query)
        {
            var result = await SendAsync<Unit>(BuildUsersRoute(query), Constant.ApiCallType.Get);
            return await _apiCallHelper.GetQueryResult<PagedResult<AdminUserListItem>>(result, "We couldn't load users right now. Please try again.");
        }

        public async Task<QueryResult<IReadOnlyList<string>>> GetRolesAsync()
        {
            var result = await SendAsync<Unit>(Constant.AdminUsers.Roles, Constant.ApiCallType.Get);
            return await _apiCallHelper.GetQueryResult<IReadOnlyList<string>>(result, "We couldn't load roles right now. Please try again.");
        }

        public async Task<QueryResult<AdminUserDetails>> GetByIdAsync(string id)
        {
            var result = await SendAsync<Unit>($"{Constant.AdminUsers.Base}/{Uri.EscapeDataString(id)}", Constant.ApiCallType.Get);
            return await _apiCallHelper.GetQueryResult<AdminUserDetails>(result, "We couldn't load this user right now. Please try again.");
        }

        public async Task<ServiceResponse<AdminUserDetails>> UpdateRolesAsync(string id, UpdateUserRoles request)
        {
            var result = await SendAsync($"{Constant.AdminUsers.Base}/{Uri.EscapeDataString(id)}/roles", Constant.ApiCallType.Update, request);
            return await _apiCallHelper.GetMutationResponse<AdminUserDetails>(result, "We couldn't update user roles right now. Please try again.");
        }

        public async Task<ServiceResponse<AdminUserDetails>> LockAsync(string id, UserLockRequest request)
        {
            var result = await SendAsync($"{Constant.AdminUsers.Base}/{Uri.EscapeDataString(id)}/lock", Constant.ApiCallType.Post, request);
            return await _apiCallHelper.GetMutationResponse<AdminUserDetails>(result, "We couldn't lock this user right now. Please try again.");
        }

        public async Task<ServiceResponse<AdminUserDetails>> UnlockAsync(string id)
        {
            var result = await SendAsync<Unit>($"{Constant.AdminUsers.Base}/{Uri.EscapeDataString(id)}/unlock", Constant.ApiCallType.Post);
            return await _apiCallHelper.GetMutationResponse<AdminUserDetails>(result, "We couldn't unlock this user right now. Please try again.");
        }

        public async Task<ServiceResponse<AdminUserDetails>> ConfirmEmailAsync(string id)
        {
            var result = await SendAsync<Unit>($"{Constant.AdminUsers.Base}/{Uri.EscapeDataString(id)}/confirm-email", Constant.ApiCallType.Post);
            return await _apiCallHelper.GetMutationResponse<AdminUserDetails>(result, "We couldn't confirm this email right now. Please try again.");
        }

        public async Task<ServiceResponse<AdminUserDetails>> RequirePasswordChangeAsync(string id)
        {
            var result = await SendAsync<Unit>($"{Constant.AdminUsers.Base}/{Uri.EscapeDataString(id)}/require-password-change", Constant.ApiCallType.Post);
            return await _apiCallHelper.GetMutationResponse<AdminUserDetails>(result, "We couldn't require a password change right now. Please try again.");
        }

        public async Task<ServiceResponse<AdminUserDetails>> DeactivateAsync(string id)
        {
            var client = await _httpClientHelper.GetPrivateClientAsync();
            var result = await _apiCallHelper.ApiCallTypeCall<Unit>(new ApiCall
            {
                Client = client,
                Route = Constant.AdminUsers.Base,
                Type = Constant.ApiCallType.Delete,
                Id = id,
            });

            return await _apiCallHelper.GetMutationResponse<AdminUserDetails>(result, "We couldn't deactivate this user right now. Please try again.");
        }

        private async Task<HttpResponseMessage> SendAsync<TModel>(string route, string type)
        {
            return await SendAsync<TModel>(route, type, default);
        }

        private async Task<HttpResponseMessage> SendAsync<TModel>(string route, string type, TModel? model)
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

        private static string BuildUsersRoute(AdminUserQuery query)
        {
            var parameters = new List<string>
            {
                $"pageNumber={Math.Max(1, query.PageNumber)}",
                $"pageSize={Math.Clamp(query.PageSize, 1, 100)}",
            };

            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                parameters.Add($"searchTerm={Uri.EscapeDataString(query.SearchTerm.Trim())}");
            }

            if (!string.IsNullOrWhiteSpace(query.Role))
            {
                parameters.Add($"role={Uri.EscapeDataString(query.Role.Trim())}");
            }

            if (query.Locked.HasValue)
            {
                parameters.Add($"locked={query.Locked.Value.ToString().ToLowerInvariant()}");
            }

            return $"{Constant.AdminUsers.Base}?{string.Join("&", parameters)}";
        }
    }
}
