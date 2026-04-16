namespace BlazorShop.Web.Shared.Models.Notifications
{
    using BlazorShop.Web.Shared.Toast;

    public sealed class AppNotification
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        public string Heading { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;

        public ToastLevel Level { get; init; } = ToastLevel.Info;

        public NotificationKind Kind { get; init; } = NotificationKind.General;

        public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

        public bool IsRead { get; set; }

        public string? Link { get; init; }
    }
}