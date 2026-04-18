namespace BlazorShop.Tests.Presentation.Storefront
{
    using BlazorShop.Storefront.Options;
    using BlazorShop.Storefront.Services;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Options;

    using Xunit;

    public class StorefrontPublicUrlResolverTests
    {
        [Fact]
        public void ResolveBaseUrl_PrefersPublicUrlOptionBeforeSeoBaseUrl()
        {
            var resolver = CreateResolver(requestContext: null, configuredOptionBaseUrl: "https://fallback.example/");

            var result = resolver.ResolveBaseUrl("https://shop.example.com/store");

            Assert.Equal("https://fallback.example/", result);
        }

        [Fact]
        public void ResolveAbsoluteUrl_FallsBackToCurrentRequestWhenConfiguredBaseUrlMissing()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("localhost", 5017);

            var resolver = CreateResolver(httpContext, configuredOptionBaseUrl: null);

            var result = resolver.ResolveAbsoluteUrl("/sitemap.xml");

            Assert.Equal("https://localhost:5017/sitemap.xml", result);
        }

        private static StorefrontPublicUrlResolver CreateResolver(HttpContext? requestContext, string? configuredOptionBaseUrl)
        {
            return new StorefrontPublicUrlResolver(
                new HttpContextAccessor { HttpContext = requestContext },
                Options.Create(new StorefrontPublicUrlOptions { BaseUrl = configuredOptionBaseUrl }));
        }
    }
}