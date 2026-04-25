namespace BlazorShop.Application.DTOs.Admin.Settings
{
    public class AdminSettingsDto
    {
        public StoreSettingsDto Store { get; set; } = new();

        public OrderSettingsDto Orders { get; set; } = new();

        public NotificationSettingsDto Notifications { get; set; } = new();

        public SystemSettingsDto System { get; set; } = new();
    }
}
