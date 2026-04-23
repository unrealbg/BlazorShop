namespace BlazorShop.Web.Authentication.Providers
{
    public interface IAuthenticationSessionSyncService : IAsyncDisposable
    {
        Task InitializeAsync();
    }
}