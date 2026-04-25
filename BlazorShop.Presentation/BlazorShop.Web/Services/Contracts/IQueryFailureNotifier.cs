namespace BlazorShop.Web.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Toast;

    public interface IQueryFailureNotifier
    {
        bool TryNotifyFailure<T>(QueryResult<T> result, string heading = "Error", ToastPosition position = ToastPosition.TopRight, bool showToast = true);
    }
}