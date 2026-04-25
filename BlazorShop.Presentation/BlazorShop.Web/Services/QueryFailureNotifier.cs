namespace BlazorShop.Web.Services
{
    using BlazorShop.Web.Services.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Notifications;
    using BlazorShop.Web.Shared.Services.Contracts;
    using BlazorShop.Web.Shared.Toast;

    public sealed class QueryFailureNotifier : IQueryFailureNotifier
    {
        private static readonly TimeSpan NotificationCooldown = TimeSpan.FromSeconds(5);

        private readonly INotificationService _notificationService;
        private DateTimeOffset _lastNotificationAtUtc = DateTimeOffset.MinValue;

        public QueryFailureNotifier(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public bool TryNotifyFailure<T>(QueryResult<T> result, string heading = "Error", ToastPosition position = ToastPosition.TopRight, bool showToast = true)
        {
            if (result.Success)
            {
                return false;
            }

            if (!showToast)
            {
                return true;
            }

            var now = DateTimeOffset.UtcNow;
            if (now - _lastNotificationAtUtc < NotificationCooldown)
            {
                return true;
            }

            _lastNotificationAtUtc = now;

            _notificationService.Notify(new NotificationRequest
            {
                Level = ToastLevel.Error,
                Kind = NotificationKind.General,
                Heading = heading,
                Message = FeedbackMessageResolver.ResolveQueryFailure(result),
                IconClass = ToastIcon.Error,
                Position = position,
            });

            return true;
        }
    }
}