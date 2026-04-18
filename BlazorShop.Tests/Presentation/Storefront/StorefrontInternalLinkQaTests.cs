namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;

    using BlazorShop.Storefront.Services;

    using Microsoft.AspNetCore.Mvc.Testing;

    using Xunit;

    public class StorefrontInternalLinkQaTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public StorefrontInternalLinkQaTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        public static TheoryData<LinkExpectation> ListingSurfaces =>
        [
            new(StorefrontRoutes.Home, [StorefrontRoutes.Category("sneakers"), StorefrontRoutes.Product("metro-runner"), StorefrontRoutes.Product("trail-runner")]),
            new(StorefrontRoutes.NewReleases, [StorefrontRoutes.Category("sneakers"), StorefrontRoutes.Product("metro-runner"), StorefrontRoutes.Product("trail-runner")]),
            new(StorefrontRoutes.TodaysDeals, [StorefrontRoutes.Category("sneakers"), StorefrontRoutes.Product("metro-runner"), StorefrontRoutes.Product("trail-runner")]),
            new(StorefrontRoutes.Category("sneakers"), [StorefrontRoutes.Product("metro-runner"), StorefrontRoutes.Product("trail-runner")]),
        ];

        public static TheoryData<string> CoreSurfaces =>
        [
            StorefrontRoutes.Home,
            StorefrontRoutes.About,
            StorefrontRoutes.CustomerService,
            StorefrontRoutes.NewReleases,
            StorefrontRoutes.Category("sneakers"),
            StorefrontRoutes.Product("metro-runner"),
        ];

        [Theory]
        [MemberData(nameof(ListingSurfaces))]
        public async Task ListingSurfaces_ExposeCrawlableProductAndCategoryAnchors(LinkExpectation expectation)
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync(expectation.Path);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.All(expectation.RequiredHrefs, href => Assert.Contains(href, document.InternalAnchorHrefs));
        }

        [Fact]
        public async Task ProductPages_ExposeBreadcrumbAndCategoryReturnLinks()
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync(StorefrontRoutes.Product("metro-runner"));
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(StorefrontRoutes.Home, document.InternalAnchorHrefs);
            Assert.Contains(StorefrontRoutes.Category("sneakers"), document.InternalAnchorHrefs);
            Assert.Contains(StorefrontRoutes.NewReleases, document.InternalAnchorHrefs);
            Assert.Contains(StorefrontRoutes.TodaysDeals, document.InternalAnchorHrefs);
        }

        [Theory]
        [MemberData(nameof(CoreSurfaces))]
        public async Task CoreSurfaces_AvoidObviousBrokenHrefPatterns(string path)
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync(path);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(document.BrokenAnchorHrefs);
        }

        public sealed record LinkExpectation(string Path, IReadOnlyList<string> RequiredHrefs);
    }
}