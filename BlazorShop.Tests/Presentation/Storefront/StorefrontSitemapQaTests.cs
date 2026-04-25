namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;

    using BlazorShop.Storefront.Services;

    using Microsoft.AspNetCore.Mvc.Testing;

    using Xunit;

    public class StorefrontSitemapQaTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public StorefrontSitemapQaTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task SitemapEndpoint_ReturnsAbsoluteCanonicalRoutesWithValidXml()
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync(StorefrontRoutes.Sitemap);
            var document = await StorefrontSitemapAuditDocument.CreateAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
            Assert.True(document.LocationsAreAbsolute);
            Assert.Contains(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.Home), document.Locations);
            Assert.Contains(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.About), document.Locations);
            Assert.Contains(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.Faq), document.Locations);
            Assert.Contains(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.Privacy), document.Locations);
            Assert.Contains(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.Terms), document.Locations);
            Assert.Contains(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.CustomerService), document.Locations);
            Assert.Contains(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.NewReleases), document.Locations);
            Assert.Contains(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.TodaysDeals), document.Locations);
            Assert.Contains(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.Category("sneakers")), document.Locations);
            Assert.Contains(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.Product("metro-runner")), document.Locations);
        }

        [Fact]
        public async Task SitemapEndpoint_ExcludesNonPublicLegacyAndRedirectSourceRoutes()
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync(StorefrontRoutes.Sitemap);
            var document = await StorefrontSitemapAuditDocument.CreateAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain(document.Locations, location => location.Contains("/admin/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(document.Locations, location => location.Contains("/api/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(document.Locations, location => location.Contains(Guid.Empty.ToString(), StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(StorefrontSeoAuditScenario.AbsoluteUrl("/product/legacy-runner"), document.Locations);
            Assert.DoesNotContain(StorefrontSeoAuditScenario.AbsoluteUrl("/category/legacy-sneakers"), document.Locations);
            Assert.DoesNotContain(StorefrontSeoAuditScenario.AbsoluteUrl("/legacy-sale"), document.Locations);
            Assert.DoesNotContain(StorefrontSeoAuditScenario.AbsoluteUrl("/product/draft-only"), document.Locations);
            Assert.DoesNotContain(StorefrontSeoAuditScenario.AbsoluteUrl("/category/private-staging"), document.Locations);
        }

        [Fact]
        public async Task SitemapEndpoint_LastModifiedValuesFollowPublishedFeed()
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync(StorefrontRoutes.Sitemap);
            var document = await StorefrontSitemapAuditDocument.CreateAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero), document.GetLastModified(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.Home)));
            Assert.Equal(new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero), document.GetLastModified(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.NewReleases)));
            Assert.Equal(new DateTimeOffset(2026, 4, 16, 0, 0, 0, TimeSpan.Zero), document.GetLastModified(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.TodaysDeals)));
            Assert.Equal(new DateTimeOffset(2026, 4, 14, 0, 0, 0, TimeSpan.Zero), document.GetLastModified(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.Category("sneakers"))));
            Assert.Equal(new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero), document.GetLastModified(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontRoutes.Product("metro-runner"))));
        }
    }
}