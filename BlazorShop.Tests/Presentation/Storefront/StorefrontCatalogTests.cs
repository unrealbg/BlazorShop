namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Collections.Concurrent;
    using System.Net;

    using BlazorShop.Storefront.Services;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Product;

    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.WebUtilities;

    using Xunit;

    public sealed class StorefrontCatalogTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
    {
        [Theory]
        [InlineData(null, 1, false)]
        [InlineData("", 1, false)]
        [InlineData("0", 1, false)]
        [InlineData("-1", 1, false)]
        [InlineData("bad", 1, false)]
        [InlineData("2", 2, false)]
        [InlineData("0002", 2, false)]
        [InlineData("89478485", 89478485, false)]
        [InlineData("89478486", 1, true)]
        [InlineData("999999999999999999999999999", 1, true)]
        public void Query_PagesCannotOverflowApiOffsets(string? input, int expectedPage, bool invalid)
        {
            var query = StorefrontCatalogQuery.Parse(null, null, null, input);
            Assert.Equal(expectedPage, query.PageNumber);
            Assert.Equal(invalid, query.IsInvalid);
        }

        [Theory]
        [InlineData("newest", ProductCatalogSortBy.Newest)]
        [InlineData("oldest", ProductCatalogSortBy.Oldest)]
        [InlineData("price-low-to-high", ProductCatalogSortBy.PriceLowToHigh)]
        [InlineData("price-high-to-low", ProductCatalogSortBy.PriceHighToLow)]
        [InlineData("name-ascending", ProductCatalogSortBy.NameAscending)]
        [InlineData("name-descending", ProductCatalogSortBy.NameDescending)]
        [InlineData("featured", ProductCatalogSortBy.Newest)]
        [InlineData("999", ProductCatalogSortBy.Newest)]
        public void Query_OnlySupportedSortsReachApi(string sort, ProductCatalogSortBy expected)
        {
            Assert.Equal(expected, StorefrontCatalogQuery.Parse(null, null, sort, null).ToApiQuery().SortBy);
        }

        [Fact]
        public void Query_EncodesRoundTripAndPreservesFilterAndPage()
        {
            var category = Guid.NewGuid();
            var query = StorefrontCatalogQuery.Parse("  blue & red / 雨 + ?#  ", category.ToString(), "NAME-DESCENDING", "5");
            var url = query.Url(6);
            var parameters = QueryHelpers.ParseQuery(new Uri("https://example.test" + url).Query);
            Assert.Equal("blue & red / 雨 + ?#", parameters["q"]);
            Assert.Equal(category.ToString(), parameters["category"]);
            Assert.Equal("name-descending", parameters["sort"]);
            Assert.Equal("6", parameters["page"]);
            Assert.Equal([1, 3, 4, 5, 6, 7, 10], query.PaginationPages(10));
            Assert.DoesNotContain("page=", query.Url(1), StringComparison.Ordinal);
            Assert.Equal("/catalog", StorefrontCatalogQuery.Parse("  ", null, null, null).Url());
            Assert.True(StorefrontCatalogQuery.Parse(null, "not-a-category", null, null).IsInvalid);
            Assert.True(StorefrontCatalogQuery.Parse(new string('x', 201), null, null, null).IsInvalid);
        }

        [Fact]
        public async Task Catalog_SsrQueryAndCanonicalPreserveFiltersAndPagination()
        {
            var requests = new ConcurrentBag<Uri>();
            var scenario = new StorefrontSeoAuditScenario();
            var category = scenario.Categories[0].Id;
            using var client = StorefrontSeoAuditClientFactory.CreateClient(factory, new StorefrontSeoAuditScenario
            {
                RequestObserver = requests.Add,
                CatalogPage = new PagedResult<GetCatalogProduct> { Items = scenario.CatalogPage.Items, PageNumber = 2, PageSize = 24, TotalCount = 27 },
            });
            var query = StorefrontCatalogQuery.Parse("runner & 雨", category.ToString(), "price-low-to-high", "2");
            using var response = await client.GetAsync(query.Url());
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("noindex,follow", document.RobotsMetaContent);
            Assert.Equal("noindex, follow", Assert.Single(response.Headers.GetValues("X-Robots-Tag")));
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.True(response.Headers.CacheControl?.Private);
            Assert.Equal(new Uri(StorefrontSeoAuditScenario.AbsoluteUrl(query.Url())).AbsoluteUri, Assert.Single(document.CanonicalUrls));
            Assert.Contains(query.Url(1), document.AnchorHrefs);
            Assert.Contains("Showing 25", document.Html, StringComparison.Ordinal);
            Assert.Contains("27 products", document.Html, StringComparison.Ordinal);
            Assert.Empty(document.JsonLdBlocks);
            Assert.DoesNotContain("runner", document.GetOpenGraphProperty("og:description") ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            var apiQuery = QueryHelpers.ParseQuery(Assert.Single(requests, uri => uri.AbsolutePath.EndsWith("/catalog/products", StringComparison.Ordinal)).Query);
            Assert.Equal("runner & 雨", apiQuery["searchTerm"]);
            Assert.Equal(category.ToString(), apiQuery["categoryId"]);
            Assert.Equal("2", apiQuery["pageNumber"]);
            Assert.Equal("24", apiQuery["pageSize"]);
            Assert.Equal("PriceLowToHigh", apiQuery["sortBy"]);
            Assert.DoesNotContain(requests, uri => uri.AbsolutePath.Contains("refresh-token", StringComparison.Ordinal));
            Assert.Contains("method=\"get\" action=\"/catalog\"", document.Html, StringComparison.Ordinal);
            Assert.DoesNotContain("name=\"page\"", document.Html, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("/catalog", "Shop all products")]
        [InlineData("/catalog?q=nothing", "No products match")]
        public async Task Catalog_HealthyEmptyIs200AndNotInSitemap(string route, string expected)
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(factory, new StorefrontSeoAuditScenario
            {
                CatalogPage = new PagedResult<GetCatalogProduct> { PageNumber = 1, PageSize = 24 },
            });
            using var response = await client.GetAsync(route);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(expected, document.Html, StringComparison.Ordinal);
            Assert.Contains("0 products", document.Html, StringComparison.Ordinal);
            Assert.Equal("noindex,follow", document.RobotsMetaContent);
            Assert.DoesNotContain("Catalog pagination", document.Html, StringComparison.Ordinal);
            using var sitemap = await client.GetAsync("/sitemap.xml");
            Assert.DoesNotContain("/catalog", await sitemap.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("/catalog?category=invalid")]
        [InlineData("/catalog?category=99999999-9999-9999-9999-999999999999")]
        [InlineData("/catalog?page=2")]
        [InlineData("/catalog?page=9999999999999999999999")]
        public async Task Catalog_InvalidOrOutOfRangeHas404WithoutCanonical(string route)
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(factory, new StorefrontSeoAuditScenario
            {
                CatalogPage = new PagedResult<GetCatalogProduct> { PageNumber = 2, PageSize = 24, TotalCount = 3 },
            });
            using var response = await client.GetAsync(route);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Equal("noindex,nofollow", document.RobotsMetaContent);
            Assert.Empty(document.CanonicalUrls);
            Assert.DoesNotContain("bs-v2-product-card", document.Html, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Catalog_ApiFailureIs503NotEmpty(bool categoryFailure)
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(factory, new StorefrontSeoAuditScenario
            {
                CategoriesStatusCode = categoryFailure ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK,
                CatalogProductsStatusCode = categoryFailure ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable,
            });
            using var response = await client.GetAsync("/catalog?q=private-term");
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.Equal("noindex,nofollow", document.RobotsMetaContent);
            Assert.Empty(document.CanonicalUrls);
            Assert.Empty(document.JsonLdBlocks);
            Assert.Contains("Try again", document.Html, StringComparison.Ordinal);
            Assert.DoesNotContain("0 products", document.Html, StringComparison.Ordinal);
            Assert.DoesNotContain("No products match", document.Html, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Catalog_InconsistentPagingMetadataCannotMasqueradeAsEmptyResults()
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(factory, new StorefrontSeoAuditScenario
            {
                CatalogPage = new PagedResult<GetCatalogProduct> { PageNumber = 1, PageSize = 24, TotalCount = 10 },
            });
            using var response = await client.GetAsync("/catalog");
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.DoesNotContain("No products match", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }

        [Fact]
        public async Task Catalog_EncodesUntrustedSearchAndNormalizesUnsupportedControls()
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(factory);
            var query = "<img src=x onerror=alert(1)>";
            using var response = await client.GetAsync($"/catalog?q={Uri.EscapeDataString(query)}&sort=featured&page=-1");
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("&lt;img", document.Html, StringComparison.Ordinal);
            Assert.DoesNotContain(query, document.Html, StringComparison.Ordinal);
            Assert.Contains("An unsupported sort or page was reset", document.Html, StringComparison.Ordinal);
            Assert.Equal(new Uri(StorefrontSeoAuditScenario.AbsoluteUrl(StorefrontCatalogQuery.Parse(query, null, null, null).Url())).AbsoluteUri, Assert.Single(document.CanonicalUrls));
        }
    }
}
