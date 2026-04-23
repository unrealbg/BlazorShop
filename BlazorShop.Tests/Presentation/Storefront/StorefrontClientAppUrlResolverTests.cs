namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Collections.Generic;

    using BlazorShop.Application.Options;
    using BlazorShop.Storefront.Services;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Options;

    using Xunit;

    public class StorefrontClientAppUrlResolverTests
    {
        [Fact]
        public void ResolveBaseUrl_PrefersServiceDiscoveryBeforeConfiguredBaseUrl()
        {
            var resolver = CreateResolver(
                configuredBaseUrl: "https://account.example.com/",
                configurationValues: new Dictionary<string, string?>
                {
                    ["Services:adminclient:https:0"] = "https://discovered.example.com/",
                });

            var result = resolver.ResolveBaseUrl();

            Assert.Equal("https://discovered.example.com/", result);
        }

        [Fact]
        public void ResolveUrl_ReturnsRelativePathWhenNoBaseUrlIsAvailable()
        {
            var resolver = CreateResolver(configuredBaseUrl: string.Empty);

            var result = resolver.ResolveUrl("/authentication/login/account");

            Assert.Equal("/authentication/login/account", result);
        }

        private static StorefrontClientAppUrlResolver CreateResolver(
            string? configuredBaseUrl,
            IDictionary<string, string?>? configurationValues = null)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configurationValues ?? new Dictionary<string, string?>())
                .Build();

            return new StorefrontClientAppUrlResolver(
                configuration,
                Options.Create(new ClientAppOptions { BaseUrl = configuredBaseUrl ?? string.Empty }));
        }
    }
}