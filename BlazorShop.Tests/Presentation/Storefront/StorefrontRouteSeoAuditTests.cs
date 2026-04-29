namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;

    using BlazorShop.Storefront.Services;

    using Microsoft.AspNetCore.Mvc.Testing;

    using Xunit;

    public class StorefrontRouteSeoAuditTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public StorefrontRouteSeoAuditTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        public static TheoryData<RouteExpectation> SuccessfulRoutes =>
        [
            new(StorefrontRoutes.Home, StorefrontRoutes.Home, ["Organization", "WebSite"]),
            new(StorefrontRoutes.About, StorefrontRoutes.About, ["WebPage"]),
            new(StorefrontRoutes.Faq, StorefrontRoutes.Faq, ["FAQPage"]),
            new(StorefrontRoutes.Privacy, StorefrontRoutes.Privacy, ["WebPage"]),
            new(StorefrontRoutes.Terms, StorefrontRoutes.Terms, ["WebPage"]),
            new(StorefrontRoutes.CustomerService, StorefrontRoutes.CustomerService, ["WebPage"]),
            new(StorefrontRoutes.NewReleases, StorefrontRoutes.NewReleases, ["CollectionPage"]),
            new(StorefrontRoutes.TodaysDeals, StorefrontRoutes.TodaysDeals, ["CollectionPage"]),
            new(StorefrontRoutes.Category("sneakers"), StorefrontRoutes.Category("sneakers"), ["CollectionPage", "BreadcrumbList"]),
            new(StorefrontRoutes.Product("metro-runner"), StorefrontRoutes.Product("metro-runner"), ["Product", "Offer", "BreadcrumbList"]),
        ];

        public static TheoryData<CanonicalExpectation> CanonicalRoutes =>
        [
            new(StorefrontRoutes.Home, StorefrontRoutes.Home),
            new(StorefrontRoutes.About, StorefrontRoutes.About),
            new(StorefrontRoutes.Faq, StorefrontRoutes.Faq),
            new(StorefrontRoutes.Privacy, StorefrontRoutes.Privacy),
            new(StorefrontRoutes.Terms, StorefrontRoutes.Terms),
            new(StorefrontRoutes.CustomerService, StorefrontRoutes.CustomerService),
            new(StorefrontRoutes.NewReleases, StorefrontRoutes.NewReleases),
            new(StorefrontRoutes.TodaysDeals, StorefrontRoutes.TodaysDeals),
            new(StorefrontRoutes.Category("sneakers"), StorefrontRoutes.Category("sneakers")),
            new(StorefrontRoutes.Product("metro-runner"), StorefrontRoutes.Product("metro-runner")),
        ];

        public static TheoryData<string> MissingRoutes =>
        [
            "/missing-storefront-page",
            StorefrontRoutes.Category("missing-category"),
            StorefrontRoutes.Product("missing-product"),
        ];

        public static IEnumerable<object[]> UnavailableRoutes()
        {
            yield return [StorefrontRoutes.Home, new StorefrontSeoAuditScenario { CategoriesStatusCode = HttpStatusCode.ServiceUnavailable }];
            yield return [StorefrontRoutes.NewReleases, new StorefrontSeoAuditScenario { CatalogProductsStatusCode = HttpStatusCode.ServiceUnavailable }];
            yield return [StorefrontRoutes.TodaysDeals, new StorefrontSeoAuditScenario { CatalogProductsStatusCode = HttpStatusCode.ServiceUnavailable }];
            yield return [StorefrontRoutes.Category("sneakers"), new StorefrontSeoAuditScenario { CategoryPageStatusCode = HttpStatusCode.ServiceUnavailable }];
            yield return [StorefrontRoutes.Product("metro-runner"), new StorefrontSeoAuditScenario { ProductPageStatusCode = HttpStatusCode.ServiceUnavailable }];
        }

        [Theory]
        [MemberData(nameof(SuccessfulRoutes))]
        public async Task SuccessfulPublicRoutes_ReturnExpectedStatusCanonicalOgAndStructuredData(RouteExpectation expectation)
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync(expectation.Path);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
            Assert.Single(document.CanonicalUrls);
            Assert.Equal(StorefrontSeoAuditScenario.AbsoluteUrl(expectation.ExpectedCanonicalPath), document.CanonicalUrls[0]);
            Assert.Equal("index,follow", document.RobotsMetaContent);
            Assert.NotNull(document.GetOpenGraphProperty("og:title"));
            Assert.NotNull(document.GetOpenGraphProperty("og:description"));
            Assert.NotNull(document.GetOpenGraphProperty("og:image"));
            Assert.NotNull(document.GetOpenGraphProperty("og:site_name"));
            Assert.NotEmpty(document.JsonLdBlocks);
            Assert.All(expectation.ExpectedSchemaTypes, schemaType => Assert.True(document.HasSchemaType(schemaType), $"Expected schema type '{schemaType}' on {expectation.Path}."));
        }

        [Theory]
        [MemberData(nameof(CanonicalRoutes))]
        public async Task CanonicalRoutes_EmitExactlyOneExpectedCanonical(CanonicalExpectation expectation)
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync(expectation.Path);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Single(document.CanonicalUrls);
            Assert.Equal(StorefrontSeoAuditScenario.AbsoluteUrl(expectation.ExpectedCanonicalPath), document.CanonicalUrls[0]);
        }

        [Theory]
        [MemberData(nameof(MissingRoutes))]
        public async Task MissingRoutes_Return404WithoutCanonicalOpenGraphOrStructuredData(string path)
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync(path);
            var html = await response.Content.ReadAsStringAsync();
            var document = StorefrontHtmlAuditDocument.Create(html);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("no-store, no-cache, max-age=0", response.Headers.CacheControl?.ToString());
            Assert.True(response.Headers.TryGetValues("X-Robots-Tag", out var robotsHeaderValues));
            Assert.Contains("noindex, nofollow", robotsHeaderValues);
            Assert.Contains("bs-storefront-header", html);
            Assert.True(
                document.RobotsMetaContent is null || string.Equals(document.RobotsMetaContent, "noindex,nofollow", StringComparison.Ordinal),
                $"Unexpected 404 robots meta value '{document.RobotsMetaContent}'.");
            Assert.Empty(document.CanonicalUrls);
            Assert.False(document.HasAnyOpenGraphTags);
            Assert.Empty(document.JsonLdBlocks);
            Assert.Null(response.Headers.RetryAfter);
        }

        [Theory]
        [MemberData(nameof(UnavailableRoutes))]
        public async Task UnavailableRoutes_Return503WithoutCanonicalOpenGraphOrStructuredData(string path, StorefrontSeoAuditScenario scenario)
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory, scenario);

            using var response = await client.GetAsync(path);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("no-store, no-cache, max-age=0", response.Headers.CacheControl?.ToString());
            Assert.Equal(TimeSpan.FromMinutes(10), response.Headers.RetryAfter?.Delta);
            Assert.True(response.Headers.TryGetValues("X-Robots-Tag", out var robotsHeaderValues));
            Assert.Contains("noindex, nofollow", robotsHeaderValues);
            Assert.Equal("noindex,nofollow", document.RobotsMetaContent);
            Assert.Empty(document.CanonicalUrls);
            Assert.False(document.HasAnyOpenGraphTags);
            Assert.Empty(document.JsonLdBlocks);
        }

        [Fact]
        public async Task ProductPage_StructuredData_LeavesUnsupportedFieldsAbsent()
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync(StorefrontRoutes.Product("metro-runner"));
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(document.HasSchemaType("Product"));
            Assert.True(document.HasSchemaType("Offer"));
            Assert.False(document.ContainsJsonProperty("aggregateRating"));
            Assert.False(document.ContainsJsonProperty("review"));
            Assert.False(document.ContainsJsonProperty("brand"));
            Assert.False(document.ContainsJsonProperty("availability"));
            Assert.False(document.ContainsJsonProperty("priceCurrency"));
        }

        public sealed record CanonicalExpectation(string Path, string ExpectedCanonicalPath);

        public sealed record RouteExpectation(string Path, string ExpectedCanonicalPath, IReadOnlyList<string> ExpectedSchemaTypes);
    }
}
