namespace BlazorShop.Web.Shared.Models.Notifications
{
    using BlazorShop.Web.Shared.Toast;

    public sealed class NotificationRequest
    {
        public string Heading { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;

        public ToastLevel Level { get; init; } = ToastLevel.Info;

        public NotificationKind Kind { get; init; } = NotificationKind.General;

        public ToastIcon IconClass { get; init; } = ToastIcon.Default;

        public ToastPosition Position { get; init; } = ToastPosition.TopRight;

        public bool Persist { get; init; }

        public int Duration { get; init; } = 5000;

        public bool ShowToast { get; init; } = true;

        public bool AddToInbox { get; init; } = true;

        public string? Link { get; init; }
    }
}