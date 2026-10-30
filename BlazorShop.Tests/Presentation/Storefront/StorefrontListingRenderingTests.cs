namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Collections.Concurrent;
    using System.Globalization;
    using System.Net;

    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Product;

    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.WebUtilities;

    using Xunit;

    public sealed class StorefrontListingRenderingTests(WebApplicationFactory<Program> factory)
        : IClassFixture<WebApplicationFactory<Program>>
    {
        [Theory]
        [InlineData("/new-releases", 24)]
        [InlineData("/todays-deals", 12)]
        public async Task Listing_RendersActualItemCountAndPreservesCatalogQuery(string route, int pageSize)
        {
            var requests = new ConcurrentBag<Uri>();
            var product = new StorefrontSeoAuditScenario().CatalogPage.Items[0];
            var scenario = new StorefrontSeoAuditScenario
            {
                RequestObserver = requests.Add,
                CatalogPage = new PagedResult<GetCatalogProduct>
                {
                    Items = [product],
                    PageNumber = 1,
                    PageSize = pageSize,
                    TotalCount = 99,
                },
            };
            using var client = StorefrontSeoAuditClientFactory.CreateClient(factory, scenario);
            var before = DateTime.UtcNow.AddDays(-7);
            using var response = await client.GetAsync(route);
            var after = DateTime.UtcNow.AddDays(-7);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("role=\"status\">1 product</span>", html, StringComparison.Ordinal);
            Assert.DoesNotContain("99 products", html, StringComparison.Ordinal);
            Assert.Contains($"href=\"/product/{product.Slug}\"", html, StringComparison.Ordinal);
            Assert.Contains($"data-product-id=\"{product.Id}\"", html, StringComparison.Ordinal);
            Assert.Contains("data-storefront-add-to-cart", html, StringComparison.Ordinal);
            Assert.Contains("CollectionPage", html, StringComparison.Ordinal);
            Assert.Contains($"href=\"{StorefrontSeoAuditScenario.AbsoluteUrl(route)}\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<del", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<s>", html, StringComparison.Ordinal);
            if (route == "/todays-deals")
            {
                Assert.Contains("current catalog prices", html, StringComparison.Ordinal);
                Assert.DoesNotContain("Hot Deals", html, StringComparison.Ordinal);
            }

            var request = Assert.Single(requests, uri => uri.AbsolutePath.EndsWith("/public/catalog/products", StringComparison.Ordinal));
            var query = QueryHelpers.ParseQuery(request.Query);
            Assert.Equal("1", query["pageNumber"].ToString());
            Assert.Equal(pageSize.ToString(CultureInfo.InvariantCulture), query["pageSize"].ToString());
            Assert.Equal("Newest", query["sortBy"].ToString());
            Assert.False(query.ContainsKey("searchTerm"));
            Assert.False(query.ContainsKey("categoryId"));
            if (route == "/new-releases")
            {
                var cutoff = DateTime.Parse(query["createdAfterUtc"].ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                Assert.Equal(DateTimeKind.Utc, cutoff.Kind);
                Assert.InRange(cutoff, before, after);
            }
            else
            {
                Assert.False(query.ContainsKey("createdAfterUtc"));
            }
        }

        [Theory]
        [InlineData("/new-releases", "No new releases have been published yet.")]
        [InlineData("/todays-deals", "No deal selections are available right now.")]
        public async Task Listing_EmptyResponseRendersHonestCountAndSeo(string route, string emptyMessage)
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(factory, new StorefrontSeoAuditScenario
            {
                CatalogPage = new PagedResult<GetCatalogProduct>(),
            });
            using var response = await client.GetAsync(route);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("role=\"status\">0 products</span>", html, StringComparison.Ordinal);
            Assert.Contains(emptyMessage, html, StringComparison.Ordinal);
            Assert.Contains("CollectionPage", html, StringComparison.Ordinal);
            Assert.DoesNotContain("bs-v2-product-card", html, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("/new-releases", "New releases are temporarily unavailable")]
        [InlineData("/todays-deals", "Today&#x27;s deals are temporarily unavailable")]
        public async Task Listing_UnavailableRetains503AndSuppressesSuccessContent(string route, string title)
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(factory, new StorefrontSeoAuditScenario
            {
                CatalogProductsStatusCode = HttpStatusCode.ServiceUnavailable,
            });
            using var response = await client.GetAsync(route);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Contains(title, html, StringComparison.Ordinal);
            Assert.Contains("noindex", html, StringComparison.Ordinal);
            Assert.DoesNotContain("CollectionPage", html, StringComparison.Ordinal);
            Assert.DoesNotContain("rel=\"canonical\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("bs-v2-count", html, StringComparison.Ordinal);
            Assert.DoesNotContain("bs-v2-product-card", html, StringComparison.Ordinal);
            Assert.Equal(1, html.Split("<h1", StringSplitOptions.None).Length - 1);
        }
    }
}
