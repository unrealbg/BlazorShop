namespace BlazorShop.Domain.Entities
{
    public class AdminSettings
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string StoreName { get; set; } = "BlazorShop";

        public string StoreSupportEmail { get; set; } = string.Empty;

        public string StoreSupportPhone { get; set; } = string.Empty;

        public string DefaultCurrency { get; set; } = "EUR";

        public string DefaultCulture { get; set; } = "en-US";

        public bool MaintenanceModeEnabled { get; set; }

        public string MaintenanceMessage { get; set; } = string.Empty;

        public bool AllowGuestCheckout { get; set; }

        public string DefaultShippingStatus { get; set; } = "PendingShipment";

        public bool AutoConfirmPaidOrders { get; set; }

        public string OrderReferencePrefix { get; set; } = "BS";

        public string SmtpHost { get; set; } = string.Empty;

        public string SmtpFromEmail { get; set; } = string.Empty;

        public string SmtpFromDisplayName { get; set; } = string.Empty;

        public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;

        public string? UpdatedByUserId { get; set; }
    }
}
