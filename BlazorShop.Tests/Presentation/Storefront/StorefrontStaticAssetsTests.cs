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