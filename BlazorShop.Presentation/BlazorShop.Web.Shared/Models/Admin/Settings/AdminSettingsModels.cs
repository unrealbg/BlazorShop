namespace BlazorShop.Web.Shared.Models.Admin.Settings
{
    public class AdminSettingsModel
    {
        public StoreSettingsModel Store { get; set; } = new();

        public OrderSettingsModel Orders { get; set; } = new();

        public NotificationSettingsModel Notifications { get; set; } = new();

        public SystemSettingsModel System { get; set; } = new();
    }

    public class StoreSettingsModel
    {
        public string StoreName { get; set; } = string.Empty;

        public string StoreSupportEmail { get; set; } = string.Empty;

        public string StoreSupportPhone { get; set; } = string.Empty;

        public string DefaultCurrency { get; set; } = "EUR";

        public string DefaultCulture { get; set; } = "en-US";

        public bool MaintenanceModeEnabled { get; set; }

        public string MaintenanceMessage { get; set; } = string.Empty;
    }

    public class UpdateStoreSettings : StoreSettingsModel
    {
    }

    public class OrderSettingsModel
    {
        public bool AllowGuestCheckout { get; set; }

        public bool GuestCheckoutSupported { get; set; }

        public string DefaultShippingStatus { get; set; } = "PendingShipment";

        public bool AutoConfirmPaidOrders { get; set; }

        public string OrderReferencePrefix { get; set; } = "BS";
    }

    public class UpdateOrderSettings
    {
        public bool AllowGuestCheckout { get; set; }

        public string DefaultShippingStatus { get; set; } = "PendingShipment";

        public bool AutoConfirmPaidOrders { get; set; }

        public string OrderReferencePrefix { get; set; } = "BS";
    }

    public class NotificationSettingsModel
    {
        public string SmtpHost { get; set; } = string.Empty;

        public string SmtpFromEmail { get; set; } = string.Empty;

        public string SmtpFromDisplayName { get; set; } = string.Empty;

        public bool SecretsConfigured { get; set; }
    }

    public class UpdateNotificationSettings
    {
        public string SmtpHost { get; set; } = string.Empty;

        public string SmtpFromEmail { get; set; } = string.Empty;

        public string SmtpFromDisplayName { get; set; } = string.Empty;
    }

    public class SystemSettingsModel
    {
        public DateTime UpdatedOn { get; set; }

        public string? UpdatedByUserId { get; set; }

        public string RuntimeEnvironment { get; set; } = string.Empty;

        public string FrameworkDescription { get; set; } = string.Empty;
    }
}
