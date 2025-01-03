namespace BlazorShop.Web.Authentication.Components
{
    using System.ComponentModel.DataAnnotations;

    using BlazorShop.Web.Shared.Models.Authentication;

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
                    this.ToastService.ShowSuccessToast("Registration successful.");
                    this.ToastService.ShowInfoToast("Please check your email to confirm your account.");
                    this.NavigationManager.NavigateTo("/authentication/login");
                }
                else
                {
                    this.ToastService.ShowErrorToast($"Registration failed: {response.Message}");
                }
            }
            catch (Exception ex)
            {
                this.ToastService.ShowErrorToast($"An error occurred: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
            }
        }
    }
}