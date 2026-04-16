namespace BlazorShop.Web.Authentication.Providers
{
    using Microsoft.AspNetCore.Components.Authorization;
    using Microsoft.Extensions.DependencyInjection;

    public sealed class AuthenticationStateNotifier : IAuthenticationStateNotifier
    {
        private readonly IServiceProvider _serviceProvider;

        public AuthenticationStateNotifier(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void NotifyAuthenticationState()
        {
            if (_serviceProvider.GetService<AuthenticationStateProvider>() is CustomAuthStateProvider authStateProvider)
            {
                authStateProvider.NotifyAuthenticationState();
            }
        }
    }
}