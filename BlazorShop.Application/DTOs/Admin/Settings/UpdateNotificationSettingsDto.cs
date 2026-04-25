namespace BlazorShop.Application.DTOs.Admin.Settings
{
    public class UpdateNotificationSettingsDto
    {
        public string SmtpHost { get; set; } = string.Empty;

        public string SmtpFromEmail { get; set; } = string.Empty;

        public string SmtpFromDisplayName { get; set; } = string.Empty;
    }
}
