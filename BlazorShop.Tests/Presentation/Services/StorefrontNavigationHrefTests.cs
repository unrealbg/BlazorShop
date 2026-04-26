namespace BlazorShop.Tests.Presentation.Services
{
    using BlazorShop.Web.Services;
    using BlazorShop.Web.Services.Contracts;

    using Xunit;

    public class StorefrontNavigationHrefTests
    {
        [Fact]
        public void ResolveShopHref_WhenStorefrontUrlIsConfigured_UsesConfiguredUrl()
        {
            var resolver = new StubPublicStorefrontUrlResolver("https://shop.example.com/");

            var result = StorefrontNavigationHref.ResolveShopHref(resolver);

            Assert.Equal("https://shop.example.com/", result);
        }

        [Fact]
        public void ResolveShopHref_WhenStorefrontUrlIsMissing_UsesLocalFallback()
        {
            var resolver = new StubPublicStorefrontUrlResolver(null);

            var result = StorefrontNavigationHref.ResolveShopHref(resolver);

            Assert.Equal("/", result);
        }

        [Fact]
        public void ResolveShopHref_WhenStorefrontUrlIsWhitespace_UsesLocalFallback()
        {
            var resolver = new StubPublicStorefrontUrlResolver("   ");

            var result = StorefrontNavigationHref.ResolveShopHref(resolver);

            Assert.Equal("/", result);
        }

        private sealed class StubPublicStorefrontUrlResolver : IPublicStorefrontUrlResolver
        {
            private readonly string? _storefrontUrl;

            public StubPublicStorefrontUrlResolver(string? storefrontUrl)
            {
                _storefrontUrl = storefrontUrl;
            }

            public string? Resolve()
            {
                return _storefrontUrl;
            }
        }
    }
}
