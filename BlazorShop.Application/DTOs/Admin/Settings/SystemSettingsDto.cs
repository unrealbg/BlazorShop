namespace BlazorShop.Application.DTOs.Admin.Settings
{
    public class SystemSettingsDto
    {
        public DateTime UpdatedOn { get; set; }

        public string? UpdatedByUserId { get; set; }

        public string RuntimeEnvironment { get; set; } = string.Empty;

        public string FrameworkDescription { get; set; } = string.Empty;
    }
}
