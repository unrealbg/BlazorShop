namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models.Notifications;
    using BlazorShop.Web.Shared.Toast;

    public interface INotificationService
    {
        event Action? OnChange;

        IReadOnlyList<AppNotification> Notifications { get; }

        int UnreadCount { get; }

        AppNotification Notify(NotificationRequest request);

        AppNotification NotifySuccess(string message, string heading = "", NotificationKind kind = NotificationKind.General, bool addToInbox = true, bool showToast = true, string? link = null, int duration = 5000, ToastPosition position = ToastPosition.TopRight);

        AppNotification NotifyInfo(string message, string heading = "", NotificationKind kind = NotificationKind.General, bool addToInbox = true, bool showToast = true, string? link = null, int duration = 5000, ToastPosition position = ToastPosition.TopRight);

        AppNotification NotifyWarning(string message, string heading = "", NotificationKind kind = NotificationKind.General, bool addToInbox = true, bool showToast = true, string? link = null, int duration = 5000, ToastPosition position = ToastPosition.TopRight);

        AppNotification NotifyError(string message, string heading = "", NotificationKind kind = NotificationKind.General, bool addToInbox = true, bool showToast = true, string? link = null, int duration = 5000, ToastPosition position = ToastPosition.TopRight);

        void MarkAsRead(Guid notificationId);

        void MarkAllAsRead();

        void ClearInbox();
    }
}