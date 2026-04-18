namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;

    using BlazorShop.Storefront.Services;

    using Microsoft.AspNetCore.Mvc.Testing;

    using Xunit;

    public class StorefrontRobotsQaTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public StorefrontRobotsQaTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task RobotsTxt_ReturnsExpectedDisallowRulesAndSitemapReference()
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync(StorefrontRoutes.Robots);
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains("User-agent: *", content, StringComparison.Ordinal);
            Assert.Contains("Allow: /", content, StringComparison.Ordinal);
            Assert.Contains("Disallow: /api/", content, StringComparison.Ordinal);
            Assert.Contains("Disallow: /swagger/", content, StringComparison.Ordinal);
            Assert.Contains("Disallow: /admin/", content, StringComparison.Ordinal);
            Assert.Contains("Disallow: /account/", content, StringComparison.Ordinal);
            Assert.Contains("Disallow: /authentication/", content, StringComparison.Ordinal);
            Assert.Contains($"Sitemap: {StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.Sitemap)}", content, StringComparison.Ordinal);
        }

        [Fact]
        public async Task RobotsTxt_DoesNotBlockPublicCatalogRoutes()
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync(StorefrontRoutes.Robots);
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("Disallow: /category/", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Disallow: /product/", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Disallow: /new-releases", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Disallow: /todays-deals", content, StringComparison.Ordinal);
        }
    }
}