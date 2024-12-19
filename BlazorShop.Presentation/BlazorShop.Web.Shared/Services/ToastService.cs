namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Services.Contracts;
    using BlazorShop.Web.Shared.Toast;

    public class ToastService : IToastService
    {
        public event EventHandler<ToastEventArgs>? OnShow;

        public void ShowToast(ToastLevel level, string message, string heading = "", ToastIcon iconClass = ToastIcon.Default, ToastPosition position = ToastPosition.TopRight, bool persist = false, int duration = 5000)
        {
            OnShow?.Invoke(this, new ToastEventArgs(level, message, heading, iconClass, position, persist, duration));
        }

        public void ShowToast(ToastOptions options)
        {
            this.ShowToast(options.Level, options.Message, options.Heading, options.IconClass, options.Position, options.Persist, options.Duration);
        }

        public void ClearToasts()
        {
            OnShow?.Invoke(this, new ToastEventArgs(default, null, null, default, default, false, 0));
        }

        public void ShowWarningToast(string message, string heading = "Warning", int duration = 5000)
        {
            this.ShowToast(ToastLevel.Warning, message, heading, ToastIcon.Warning, ToastPosition.TopRight, false, duration);
        }

        public void ShowSuccessToast(string message, string heading = "Success", int duration = 5000)
        {
            this.ShowToast(ToastLevel.Success, message, heading, ToastIcon.Success, ToastPosition.TopRight, false, duration);
        }

        public void ShowErrorToast(string message, string heading = "Error", int duration = 5000)
        {
            this.ShowToast(ToastLevel.Error, message, heading, ToastIcon.Error, ToastPosition.TopRight, false, duration);
        }

        public void ShowInfoToast(string message, string heading = "Info", int duration = 5000)
        {
            this.ShowToast(ToastLevel.Info, message, heading, ToastIcon.Info, ToastPosition.TopRight, false, duration);
        }
    }
}
