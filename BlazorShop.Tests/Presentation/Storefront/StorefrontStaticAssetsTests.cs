namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;

    using Microsoft.AspNetCore.Mvc.Testing;

    using Xunit;

    public class StorefrontStaticAssetsTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public StorefrontStaticAssetsTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task CssStaticAssetEndpoint_ReturnsStylesheetContent()
        {
            using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

            using var response = await client.GetAsync("/css/site.css");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task StorefrontCssStaticAssetEndpoint_ReturnsStylesheetContent()
        {
            using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

            using var response = await client.GetAsync("/css/storefront.css");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData("/css/ui-v2-storefront.css", "text/css")]
        [InlineData("/ui-v2/hero-chair.svg", "image/svg+xml")]
        public async Task UiV2StaticAssetEndpoint_ReturnsExpectedContentType(string path, string contentType)
        {
            using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using var response = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
            Assert.False(string.IsNullOrWhiteSpace(await response.Content.ReadAsStringAsync()));
        }

        [Fact]
        public async Task IconStaticAssetEndpoint_ReturnsBinaryContent()
        {
            using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

            using var response = await client.GetAsync("/icon-192.png");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
