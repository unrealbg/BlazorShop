namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Admin.Settings;

    public interface IAdminSettingsService
    {
        Task<QueryResult<AdminSettingsModel>> GetAsync();

        Task<ServiceResponse<StoreSettingsModel>> UpdateStoreAsync(UpdateStoreSettings request);

        Task<ServiceResponse<OrderSettingsModel>> UpdateOrdersAsync(UpdateOrderSettings request);

        Task<ServiceResponse<NotificationSettingsModel>> UpdateNotificationsAsync(UpdateNotificationSettings request);
    }
}
