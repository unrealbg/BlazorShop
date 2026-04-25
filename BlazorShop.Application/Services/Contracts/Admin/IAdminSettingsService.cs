namespace BlazorShop.Application.Services.Contracts.Admin
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Settings;

    public interface IAdminSettingsService
    {
        Task<AdminSettingsDto> GetAsync();

        Task<ServiceResponse<StoreSettingsDto>> UpdateStoreAsync(UpdateStoreSettingsDto request);

        Task<ServiceResponse<OrderSettingsDto>> UpdateOrdersAsync(UpdateOrderSettingsDto request);

        Task<ServiceResponse<NotificationSettingsDto>> UpdateNotificationsAsync(UpdateNotificationSettingsDto request);
    }
}
