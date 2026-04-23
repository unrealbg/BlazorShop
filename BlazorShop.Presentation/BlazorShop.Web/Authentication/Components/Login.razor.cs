namespace BlazorShop.Web.Authentication.Components
{
    using System.Security.Claims;

    using BlazorShop.Web.Authentication.Providers;
    using BlazorShop.Web.Services;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models.Authentication;
    using BlazorShop.Web.Shared.Models.Notifications;

    using Microsoft.AspNetCore.Components;

    public partial class Login
    {
        private readonly AsyncActionGate _loginGate = new();
        private string _alertType = string.Empty;

        private string _message = string.Empty;

        [Inject]
        private IAuthenticationSessionEventPublisher SessionEventPublisher { get; set; } = default!;

        [Parameter]
        public string Route { get; set; } = null!;

        public LoginUser User { get; set; } = new();

        private bool IsSubmitting => _loginGate.IsRunning;

        protected override async Task OnParametersSetAsync()
        {
            var authenticationState = await this.AuthStateProvider.GetAuthenticationStateAsync();
            if (authenticationState.User.Identity?.IsAuthenticated != true)
            {
                return;
            }

            var targetRoute = ResolveTargetRoute(authenticationState.User);
            this.NavigationManager.NavigateTo(targetRoute, replace: true);
        }

        private async Task LoginUser()
        {
            await _loginGate.RunAsync(async () =>
            {
                _message = string.Empty;
                _alertType = string.Empty;
                await InvokeAsync(StateHasChanged);

                var result = await this.AuthenticationService.LoginUser(this.User);

                if (!result.Success)
                {
                    _message = string.IsNullOrWhiteSpace(result.Message) ? "Unable to sign you in right now." : result.Message;
                    _alertType = "danger";
                    return;
                }

                await this.TokenService.StoreJwtTokenAsync(Constant.TokenStorage.Key, result.Token);

                (this.AuthStateProvider as CustomAuthStateProvider)!.NotifyAuthenticationState();
                await this.SessionEventPublisher.PublishSignedInAsync();

                var authState = await this.AuthStateProvider.GetAuthenticationStateAsync();
                var targetRoute = ResolveTargetRoute(authState.User);
                var inboxLink = targetRoute.StartsWith('/') ? targetRoute : $"/{targetRoute}";
                var successMessage = string.IsNullOrWhiteSpace(result.Message) ? "You have signed in successfully." : result.Message;

                this.NotificationService.NotifySuccess(successMessage, "Signed in", NotificationKind.Authentication, link: inboxLink);
                this.NavigationManager.NavigateTo(targetRoute);
            });
        }

        private string ResolveTargetRoute(ClaimsPrincipal user)
        {
            return ProtectedRouteRedirectResolver.ResolvePostLoginPath(
                this.Route,
                user.IsInRole(Constant.Administration.AdminRole));
        }
    }
}