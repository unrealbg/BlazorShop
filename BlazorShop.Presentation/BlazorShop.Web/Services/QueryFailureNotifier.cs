namespace BlazorShop.Web.Services
{
    using BlazorShop.Web.Services.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Services.Contracts;
    using BlazorShop.Web.Shared.Toast;

    public sealed class QueryFailureNotifier : IQueryFailureNotifier
    {
        private static readonly TimeSpan NotificationCooldown = TimeSpan.FromSeconds(5);

        private readonly IToastService _toastService;
        private DateTimeOffset _lastNotificationAtUtc = DateTimeOffset.MinValue;

        public QueryFailureNotifier(IToastService toastService)
        {
            _toastService = toastService;
        }

        public bool TryNotifyFailure<T>(QueryResult<T> result, string heading = "Error", ToastPosition position = ToastPosition.TopRight)
        {
            if (result.Success)
            {
                return false;
            }

            var now = DateTimeOffset.UtcNow;
            if (now - _lastNotificationAtUtc < NotificationCooldown)
            {
                return true;
            }

            _lastNotificationAtUtc = now;

            _toastService.ShowToast(
                ToastLevel.Error,
                result.Message,
                heading,
                ToastIcon.Error,
                position);

            return true;
        }
    }
}