namespace BlazorShop.Web.Authentication.Providers
{
    using BlazorShop.Web.Shared.Models;

    public interface IAuthenticationSessionRefresher
    {
        Task<LoginResponse?> TryRefreshAsync(string? rejectedToken = null, bool onlyIfNeeded = false);
    }
}