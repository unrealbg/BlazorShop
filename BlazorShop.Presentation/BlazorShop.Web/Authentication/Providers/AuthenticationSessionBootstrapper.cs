namespace BlazorShop.Web.Authentication.Providers;

public sealed class AuthenticationSessionBootstrapper(IAuthenticationSessionRefresher sessionRefresher) : IAuthenticationSessionBootstrapper
{
    public async Task RestoreAsync()
    {
        // The coordinator checks both the tab's JWT and the shared login/logout epoch.
        // A surviving sessionStorage JWT alone must not override a newer logout.
        await sessionRefresher.TryRefreshAsync(onlyIfNeeded: true);
    }
}
