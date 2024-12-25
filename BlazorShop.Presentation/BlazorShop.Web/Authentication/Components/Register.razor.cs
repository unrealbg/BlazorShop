namespace BlazorShop.Web.Authentication.Components
{
    using BlazorShop.Web.Shared.Models.Authentication;

    public partial class Register
    {
        private CreateUser _user = new();

        private async Task HandleRegister()
        {
            try
            {
                var response = await this.AuthenticationService.CreateUser(_user);

                if (response.Success)
                {
                    this.ToastService.ShowSuccessToast("Registration successful. You can now log in.");
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
        }
    }
}