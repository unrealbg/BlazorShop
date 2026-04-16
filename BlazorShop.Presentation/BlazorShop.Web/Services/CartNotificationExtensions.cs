namespace BlazorShop.Web.Services
{
    using BlazorShop.Web.Shared.Models.Notifications;
    using BlazorShop.Web.Shared.Services.Contracts;
    using BlazorShop.Web.Shared.Toast;

    internal static class CartNotificationExtensions
    {
        public static void NotifyCartItemAdded(this INotificationService notificationService, string? productName, ToastPosition position = ToastPosition.BottomRight)
        {
            notificationService.NotifySuccess(
                $"Product {ResolveProductName(productName)} added to cart",
                "Cart",
                NotificationKind.Order,
                addToInbox: false,
                position: position);
        }

        public static void NotifyCartQuantityIncreased(this INotificationService notificationService, string? productName, ToastPosition position = ToastPosition.BottomRight)
        {
            notificationService.NotifyInfo(
                $"Increased quantity of {ResolveProductName(productName)}",
                "Cart",
                NotificationKind.Order,
                addToInbox: false,
                position: position);
        }

        public static void NotifyCartVariantAdded(this INotificationService notificationService, string? productName, string? sizeValue, ToastPosition position = ToastPosition.BottomRight)
        {
            var variantSuffix = string.IsNullOrWhiteSpace(sizeValue)
                ? string.Empty
                : $" (size {sizeValue})";

            notificationService.NotifySuccess(
                $"Product {ResolveProductName(productName)}{variantSuffix} added to cart",
                "Cart",
                NotificationKind.Order,
                addToInbox: false,
                position: position);
        }

        private static string ResolveProductName(string? productName)
        {
            return string.IsNullOrWhiteSpace(productName) ? "product" : productName;
        }
    }
}