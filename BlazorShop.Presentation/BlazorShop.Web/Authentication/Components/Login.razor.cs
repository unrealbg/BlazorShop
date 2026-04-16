namespace BlazorShop.Web.Authentication.Components
{
    using BlazorShop.Web.Authentication.Providers;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models.Authentication;
    using BlazorShop.Web.Shared.Models.Notifications;

    using Microsoft.AspNetCore.Components;

    public partial class Login
    {
        private string _alertType = string.Empty;

        private string _message = string.Empty;

        [Parameter]
        public string Route { get; set; } = null!;

        public LoginUser User { get; set; } = new();

        private async Task LoginUser()
        {
            _message = string.Empty;
            _alertType = string.Empty;

            var result = await this.AuthenticationService.LoginUser(this.User);

            if (!result.Success)
            {
                _message = result.Message;
                _alertType = "danger";
                this.NotificationService.NotifyError(result.Message, "Login failed", NotificationKind.Authentication);
                return;
            }

            await this.TokenService.StoreJwtTokenAsync(Constant.TokenStorage.Key, result.Token);

            (this.AuthStateProvider as CustomAuthStateProvider)!.NotifyAuthenticationState();

            var targetRoute = string.IsNullOrWhiteSpace(this.Route) ? "/" : this.Route;
            var inboxLink = targetRoute.StartsWith('/') ? targetRoute : $"/{targetRoute}";
            var successMessage = string.IsNullOrWhiteSpace(result.Message) ? "You have signed in successfully." : result.Message;

            this.NotificationService.NotifySuccess(successMessage, "Signed in", NotificationKind.Authentication, link: inboxLink);
            this.NavigationManager.NavigateTo(targetRoute);
        }
    }
}