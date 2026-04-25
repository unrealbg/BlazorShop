namespace BlazorShop.Tests.Presentation.Services
{
    using BlazorShop.Web.Services;

    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;

    using Xunit;

    public class PublicStorefrontUrlResolverTests
    {
        [Fact]
        public void Resolve_WhenPublicStorefrontBaseUrlConfigured_PrefersExplicitSetting()
        {
            var resolver = CreateResolver(
                new Dictionary<string, string?>
                {
                    ["PublicStorefront:BaseUrl"] = "https://shop.example.com/",
                    ["Services:storefront:https:0"] = "https://apphost-shop.example.com/",
                },
                Environments.Production);

            var result = resolver.Resolve();

            Assert.Equal("https://shop.example.com/", result);
        }

        [Fact]
        public void Resolve_WhenServiceDiscoveryUrlExists_UsesIt()
        {
            var resolver = CreateResolver(
                new Dictionary<string, string?>
                {
                    ["Services:storefront:https:0"] = "https://apphost-shop.example.com/",
                },
                Environments.Production);

            var result = resolver.Resolve();

            Assert.Equal("https://apphost-shop.example.com/", result);
        }

        [Fact]
        public void Resolve_WhenNoStorefrontUrlIsConfiguredInDevelopment_UsesLocalFallback()
        {
            var resolver = CreateResolver(new Dictionary<string, string?>(), Environments.Development);

            var result = resolver.Resolve();

            Assert.Equal("https://localhost:18597/", result);
        }

        [Fact]
        public void Resolve_WhenNoStorefrontUrlIsConfiguredOutsideDevelopment_ReturnsNull()
        {
            var resolver = CreateResolver(new Dictionary<string, string?>(), Environments.Production);

            var result = resolver.Resolve();

            Assert.Null(result);
        }

        private static PublicStorefrontUrlResolver CreateResolver(Dictionary<string, string?> values, string environmentName)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(values)
                .Build();

            return new PublicStorefrontUrlResolver(configuration, new TestWebAssemblyHostEnvironment
            {
                BaseAddress = "https://localhost:7258/",
                Environment = environmentName,
            });
        }

        private sealed class TestWebAssemblyHostEnvironment : IWebAssemblyHostEnvironment
        {
            public string Environment { get; set; } = Environments.Production;

            public string BaseAddress { get; set; } = "https://localhost:7258/";
        }
    }
}