namespace BlazorShop.Web.Services
{
    using BlazorShop.Web.Shared.Models.Notifications;
    using BlazorShop.Web.Shared.Services.Contracts;
    using BlazorShop.Web.Shared.Toast;

    public sealed class NotificationService : INotificationService
    {
        private const int MaxInboxItems = 20;

        private readonly object _sync = new();
        private readonly List<AppNotification> _notifications = [];
        private readonly IToastService _toastService;

        public NotificationService(IToastService toastService)
        {
            _toastService = toastService;
        }

        public event Action? OnChange;

        public IReadOnlyList<AppNotification> Notifications
        {
            get
            {
                lock (_sync)
                {
                    return _notifications.ToList();
                }
            }
        }

        public int UnreadCount
        {
            get
            {
                lock (_sync)
                {
                    return _notifications.Count(x => !x.IsRead);
                }
            }
        }

        public AppNotification Notify(NotificationRequest request)
        {
            var heading = string.IsNullOrWhiteSpace(request.Heading)
                ? ResolveDefaultHeading(request.Level)
                : request.Heading;
            var message = string.IsNullOrWhiteSpace(request.Message)
                ? "An event occurred."
                : request.Message;

            var notification = new AppNotification
            {
                Heading = heading,
                Message = message,
                Level = request.Level,
                Kind = request.Kind,
                Link = request.Link,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                IsRead = false,
            };

            var inboxChanged = false;

            if (request.AddToInbox)
            {
                lock (_sync)
                {
                    _notifications.Insert(0, notification);

                    if (_notifications.Count > MaxInboxItems)
                    {
                        _notifications.RemoveRange(MaxInboxItems, _notifications.Count - MaxInboxItems);
                    }
                }

                inboxChanged = true;
            }

            if (request.ShowToast)
            {
                _toastService.ShowToast(
                    request.Level,
                    message,
                    heading,
                    request.IconClass == ToastIcon.Default ? ResolveDefaultIcon(request.Level) : request.IconClass,
                    request.Position,
                    request.Persist,
                    request.Duration);
            }

            if (inboxChanged)
            {
                OnChange?.Invoke();
            }

            return notification;
        }

        public AppNotification NotifySuccess(string message, string heading = "", NotificationKind kind = NotificationKind.General, bool addToInbox = true, bool showToast = true, string? link = null, int duration = 5000, ToastPosition position = ToastPosition.TopRight)
            => NotifyByLevel(ToastLevel.Success, message, heading, kind, addToInbox, showToast, link, duration, position);

        public AppNotification NotifyInfo(string message, string heading = "", NotificationKind kind = NotificationKind.General, bool addToInbox = true, bool showToast = true, string? link = null, int duration = 5000, ToastPosition position = ToastPosition.TopRight)
            => NotifyByLevel(ToastLevel.Info, message, heading, kind, addToInbox, showToast, link, duration, position);

        public AppNotification NotifyWarning(string message, string heading = "", NotificationKind kind = NotificationKind.General, bool addToInbox = true, bool showToast = true, string? link = null, int duration = 5000, ToastPosition position = ToastPosition.TopRight)
            => NotifyByLevel(ToastLevel.Warning, message, heading, kind, addToInbox, showToast, link, duration, position);

        public AppNotification NotifyError(string message, string heading = "", NotificationKind kind = NotificationKind.General, bool addToInbox = true, bool showToast = true, string? link = null, int duration = 5000, ToastPosition position = ToastPosition.TopRight)
            => NotifyByLevel(ToastLevel.Error, message, heading, kind, addToInbox, showToast, link, duration, position);

        public void MarkAsRead(Guid notificationId)
        {
            var changed = false;

            lock (_sync)
            {
                var notification = _notifications.FirstOrDefault(x => x.Id == notificationId);
                if (notification is null || notification.IsRead)
                {
                    return;
                }

                notification.IsRead = true;
                changed = true;
            }

            if (changed)
            {
                OnChange?.Invoke();
            }
        }

        public void MarkAllAsRead()
        {
            var changed = false;

            lock (_sync)
            {
                foreach (var notification in _notifications.Where(x => !x.IsRead))
                {
                    notification.IsRead = true;
                    changed = true;
                }
            }

            if (changed)
            {
                OnChange?.Invoke();
            }
        }

        public void ClearInbox()
        {
            lock (_sync)
            {
                if (_notifications.Count == 0)
                {
                    return;
                }

                _notifications.Clear();
            }

            OnChange?.Invoke();
        }

        private AppNotification NotifyByLevel(ToastLevel level, string message, string heading, NotificationKind kind, bool addToInbox, bool showToast, string? link, int duration, ToastPosition position)
        {
            return Notify(new NotificationRequest
            {
                Level = level,
                Heading = heading,
                Message = message,
                Kind = kind,
                AddToInbox = addToInbox,
                ShowToast = showToast,
                Link = link,
                Duration = duration,
                Position = position,
            });
        }

        private static string ResolveDefaultHeading(ToastLevel level)
        {
            return level switch
            {
                ToastLevel.Success => "Success",
                ToastLevel.Warning => "Warning",
                ToastLevel.Error => "Error",
                _ => "Info",
            };
        }

        private static ToastIcon ResolveDefaultIcon(ToastLevel level)
        {
            return level switch
            {
                ToastLevel.Success => ToastIcon.Success,
                ToastLevel.Warning => ToastIcon.Warning,
                ToastLevel.Error => ToastIcon.Error,
                _ => ToastIcon.Info,
            };
        }
    }
}