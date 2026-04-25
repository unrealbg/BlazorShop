namespace BlazorShop.Web.Authentication.Providers
{
    public interface IAuthenticatedClientStateCleaner
    {
        Task ClearAsync();
    }
}