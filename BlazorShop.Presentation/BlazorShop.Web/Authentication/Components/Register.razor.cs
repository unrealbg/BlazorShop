namespace BlazorShop.Web.Authentication.Components
{
    using System.ComponentModel.DataAnnotations;

    using BlazorShop.Web.Services;
    using BlazorShop.Web.Shared.Models.Authentication;
    using BlazorShop.Web.Shared.Models.Notifications;

    public partial class Register
    {
        private readonly AsyncActionGate _registerGate = new();
        private CreateUser _user = new();
        private string? _errorMessage;

        private bool _isLoading => _registerGate.IsRunning;

        private async Task HandleRegister()
        {
            await _registerGate.RunAsync(async () =>
            {
                _errorMessage = null;
                await InvokeAsync(StateHasChanged);

                var response = await this.AuthenticationService.CreateUser(_user);

                if (response.Success)
                {
                    this.NotificationService.NotifySuccess(
                        "Registration successful. Please check your email to confirm your account.",
                        "Registration",
                        NotificationKind.Authentication);
                    this.NavigationManager.NavigateTo("/authentication/login/account");
                    return;
                }

                _errorMessage = string.IsNullOrWhiteSpace(response.Message)
                    ? "We couldn't complete registration right now."
                    : response.Message;
            });
        }
    }
}