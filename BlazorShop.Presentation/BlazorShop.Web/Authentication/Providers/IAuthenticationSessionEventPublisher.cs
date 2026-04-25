namespace BlazorShop.Web.Authentication.Providers
{
    public interface IAuthenticationSessionEventPublisher
    {
        Task PublishSignedInAsync();

        Task PublishSignedOutAsync();

        Task PublishSessionRefreshedAsync();
    }
}