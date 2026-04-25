namespace BlazorShop.Web.Authentication.Providers
{
    public interface IAuthenticationSessionBootstrapper
    {
        Task RestoreAsync();
    }
}