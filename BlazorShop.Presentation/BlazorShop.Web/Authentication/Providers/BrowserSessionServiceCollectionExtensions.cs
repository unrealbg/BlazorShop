namespace BlazorShop.Web.Authentication.Providers;

using Microsoft.Extensions.DependencyInjection;

public static class BrowserSessionServiceCollectionExtensions
{
    public static IServiceCollection AddBrowserSessionCoordination(this IServiceCollection services)
    {
        // This registration belongs ONLY to the standalone WebAssembly host. One tab
        // needs one flight/epoch owner, including IHttpClientFactory handler scopes.
        services.AddSingleton<IAuthenticationSessionCoordinator, AuthenticationSessionCoordinator>();
        services.AddSingleton<IAuthenticationSessionRefresher, AuthenticationSessionRefresher>();
        return services;
    }
}
