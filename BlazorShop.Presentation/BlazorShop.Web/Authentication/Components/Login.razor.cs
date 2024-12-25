namespace BlazorShop.Web.Authentication.Components
{
    using BlazorShop.Web.Authentication.Providers;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models.Authentication;
    using BlazorShop.Web.Shared.Toast;

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
                this.ToastService.ShowToast(ToastLevel.Error, result.Message, "Error", ToastIcon.Error);
                return;
            }

            string cookieValue = this.TokenService.FromToken(result.Token, result.RefreshToken);
            await this.TokenService.SetCookie(
                Constant.Cookie.Name,
                cookieValue,
                Constant.Cookie.Days,
                Constant.Cookie.Path);

            (this.AuthStateProvider as CustomAuthStateProvider)!.NotifyAuthenticationState();

            this.NavigationManager.NavigateTo(this.Route == null ? "/" : this.Route, true);
        }
    }
}