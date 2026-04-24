namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Admin.Settings;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class AdminSettingsService : IAdminSettingsService
    {
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper;

        public AdminSettingsService(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper)
        {
            _httpClientHelper = httpClientHelper;
            _apiCallHelper = apiCallHelper;
        }

        public async Task<QueryResult<AdminSettingsModel>> GetAsync()
        {
            var result = await SendAsync<Unit>(Constant.AdminSettings.Base, Constant.ApiCallType.Get);
            return await _apiCallHelper.GetQueryResult<AdminSettingsModel>(result, "We couldn't load admin settings right now. Please try again.");
        }

        public async Task<ServiceResponse<StoreSettingsModel>> UpdateStoreAsync(UpdateStoreSettings request)
        {
            var result = await SendAsync(Constant.AdminSettings.Store, Constant.ApiCallType.Update, request);
            return await _apiCallHelper.GetMutationResponse<StoreSettingsModel>(result, "We couldn't update store settings right now. Please try again.");
        }

        public async Task<ServiceResponse<OrderSettingsModel>> UpdateOrdersAsync(UpdateOrderSettings request)
        {
            var result = await SendAsync(Constant.AdminSettings.Orders, Constant.ApiCallType.Update, request);
            return await _apiCallHelper.GetMutationResponse<OrderSettingsModel>(result, "We couldn't update order settings right now. Please try again.");
        }

        public async Task<ServiceResponse<NotificationSettingsModel>> UpdateNotificationsAsync(UpdateNotificationSettings request)
        {
            var result = await SendAsync(Constant.AdminSettings.Notifications, Constant.ApiCallType.Update, request);
            return await _apiCallHelper.GetMutationResponse<NotificationSettingsModel>(result, "We couldn't update notification settings right now. Please try again.");
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
    }
}
