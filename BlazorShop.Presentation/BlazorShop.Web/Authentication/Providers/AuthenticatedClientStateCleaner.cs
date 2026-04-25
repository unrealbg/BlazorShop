namespace BlazorShop.Web.Authentication.Providers
{
    using BlazorShop.Web.Shared.Services.Contracts;

    public sealed class AuthenticatedClientStateCleaner : IAuthenticatedClientStateCleaner
    {
        private readonly INotificationService _notificationService;

        public AuthenticatedClientStateCleaner(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public Task ClearAsync()
        {
            _notificationService.ClearInbox();
            return Task.CompletedTask;
        }
    }
}