namespace BlazorShop.Web.Authentication.Components
{
    using System.ComponentModel.DataAnnotations;

    using BlazorShop.Web.Shared.Models.Authentication;
    using BlazorShop.Web.Shared.Models.Notifications;

    public partial class Register
    {
        private CreateUser _user = new();
        private bool _isLoading = false;

        private async Task HandleRegister()
        {
            _isLoading = true;

            try
            {
                var response = await this.AuthenticationService.CreateUser(_user);

                if (response.Success)
                {
                    this.NotificationService.NotifySuccess(
                        "Registration successful. Please check your email to confirm your account.",
                        "Registration",
                        NotificationKind.Authentication);
                    this.NavigationManager.NavigateTo("/authentication/login");
                }
                else
                {
                    this.NotificationService.NotifyError($"Registration failed: {response.Message}", "Registration", NotificationKind.Authentication);
                }
            }
            catch (Exception ex)
            {
                this.NotificationService.NotifyError($"An error occurred: {ex.Message}", "Registration", NotificationKind.Authentication);
            }
            finally
            {
                _isLoading = false;
            }
        }
    }
}