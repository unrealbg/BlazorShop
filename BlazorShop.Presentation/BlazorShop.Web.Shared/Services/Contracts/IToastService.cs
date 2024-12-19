namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Toast;

    public interface IToastService
    {
        event EventHandler<ToastEventArgs> OnShow;

        void ShowToast(ToastLevel level, string message, string heading = "", ToastIcon iconClass = ToastIcon.Default, ToastPosition position = ToastPosition.TopRight, bool persist = false, int duration = 5000);

        void ShowToast(ToastOptions options);

        void ShowWarningToast(string message, string heading = "Warning", int duration = 5000);

        void ShowSuccessToast(string message, string heading = "Success", int duration = 5000);

        void ShowErrorToast(string message, string heading = "Error", int duration = 5000);

        void ShowInfoToast(string message, string heading = "Info", int duration = 5000);

        void ClearToasts();
    }
}
