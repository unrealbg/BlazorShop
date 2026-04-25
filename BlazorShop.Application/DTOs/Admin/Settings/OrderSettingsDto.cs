namespace BlazorShop.Application.DTOs.Admin.Settings
{
    public class OrderSettingsDto
    {
        public bool AllowGuestCheckout { get; set; }

        public bool GuestCheckoutSupported { get; set; }

        public string DefaultShippingStatus { get; set; } = "PendingShipment";

        public bool AutoConfirmPaidOrders { get; set; }

        public string OrderReferencePrefix { get; set; } = "BS";
    }
}
