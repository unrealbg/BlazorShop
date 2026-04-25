namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;

    using Xunit;

    internal static class StorefrontSeoSmokeAssertions
    {
        public static void AssertCanonicalizedHtmlPage(HttpResponseMessage response, StorefrontHtmlAuditDocument document, string expectedCanonicalUrl)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
            Assert.Single(document.CanonicalUrls);
            Assert.Equal(expectedCanonicalUrl, document.CanonicalUrls[0]);
            Assert.Equal("index,follow", document.RobotsMetaContent);
            Assert.Empty(document.BrokenAssetUrls);
        }

        public static void AssertIndexableHtmlPage(HttpResponseMessage response, StorefrontHtmlAuditDocument document, string expectedCanonicalUrl, params string[] expectedSchemaTypes)
        {
            AssertCanonicalizedHtmlPage(response, document, expectedCanonicalUrl);
            Assert.NotEmpty(document.JsonLdBlocks);
            Assert.All(expectedSchemaTypes, schemaType => Assert.True(document.HasSchemaType(schemaType), $"Expected schema type '{schemaType}' on {response.RequestMessage?.RequestUri}."));
        }

        public static void AssertNoindexedErrorSurface(HttpResponseMessage response, StorefrontHtmlAuditDocument document, HttpStatusCode expectedStatusCode)
        {
            Assert.Equal(expectedStatusCode, response.StatusCode);
            Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal("no-store, no-cache, max-age=0", response.Headers.CacheControl?.ToString());
            Assert.True(response.Headers.TryGetValues("X-Robots-Tag", out var robotsHeaderValues));
            Assert.Contains("noindex, nofollow", robotsHeaderValues);
            Assert.True(
                document.RobotsMetaContent is null || string.Equals(document.RobotsMetaContent, "noindex,nofollow", StringComparison.Ordinal),
                $"Unexpected robots meta value '{document.RobotsMetaContent}'.");
            Assert.Empty(document.CanonicalUrls);
            Assert.False(document.HasAnyOpenGraphTags);
            Assert.Empty(document.JsonLdBlocks);
        }

        public static void AssertPermanentRedirect(HttpResponseMessage response, string expectedLocation, int expectedStatusCode)
        {
            Assert.Equal((HttpStatusCode)expectedStatusCode, response.StatusCode);
            Assert.Equal(expectedLocation, response.Headers.Location?.OriginalString);
        }

        public static void AssertRobotsDocument(HttpResponseMessage response, string body, string expectedSitemapUrl)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
            Assert.Contains("User-agent: *", body, StringComparison.Ordinal);
            Assert.Contains("Allow: /", body, StringComparison.Ordinal);
            Assert.Contains($"Sitemap: {expectedSitemapUrl}", body, StringComparison.Ordinal);
        }

        public static void AssertSitemapDocument(HttpResponseMessage response, StorefrontSitemapAuditDocument document, params string[] expectedLocations)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
            Assert.True(document.LocationsAreAbsolute);
            Assert.All(expectedLocations, location => Assert.Contains(location, document.Locations));
        }
    }
}