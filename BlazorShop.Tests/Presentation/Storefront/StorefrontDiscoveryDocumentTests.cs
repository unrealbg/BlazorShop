namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Xml.Linq;

    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Storefront.Services;
    using BlazorShop.Storefront.Services.Contracts;
    using BlazorShop.Web.Shared.Models.Discovery;

    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    using Xunit;

    public class StorefrontDiscoveryDocumentTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public StorefrontDiscoveryDocumentTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task SitemapEndpoint_ReturnsExpectedPublishedRoutesAsXml()
        {
            using var client = CreateClient(new GetPublicCatalogSitemap
            {
                Categories = [new GetCategorySitemapEntry { Slug = "sneakers", LastModifiedUtc = new DateTime(2026, 4, 14, 0, 0, 0, DateTimeKind.Utc) }],
                Products = [new GetProductSitemapEntry { Slug = "metro-runner", LastModifiedUtc = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc) }],
            });

            using var response = await client.GetAsync("/sitemap.xml");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
            Assert.True(response.Headers.CacheControl?.Public);
            Assert.True(response.Headers.CacheControl?.MustRevalidate);
            Assert.Equal(TimeSpan.FromMinutes(15), response.Headers.CacheControl?.MaxAge);

            var document = XDocument.Parse(content);
            XNamespace sitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var locations = document.Root!
                .Elements(sitemapNamespace + "url")
                .Select(element => element.Element(sitemapNamespace + "loc")!.Value)
                .ToList();

            Assert.Contains("https://shop.example.com/", locations);
            Assert.Contains("https://shop.example.com/about-us", locations);
            Assert.Contains("https://shop.example.com/new-releases", locations);
            Assert.Contains("https://shop.example.com/category/sneakers", locations);
            Assert.Contains("https://shop.example.com/product/metro-runner", locations);
            Assert.DoesNotContain(locations, location => location.Contains("/category/") && location.Contains(Guid.Empty.ToString(), StringComparison.OrdinalIgnoreCase));
            Assert.Contains(document.Descendants(sitemapNamespace + "lastmod"), element => element.Value == "2026-04-15T00:00:00Z");
        }

        [Fact]
        public async Task RobotsEndpoint_ReturnsPlainTextAndSitemapReference()
        {
            using var client = CreateClient(new GetPublicCatalogSitemap());

            using var response = await client.GetAsync("/robots.txt");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
            Assert.True(response.Headers.CacheControl?.Public);
            Assert.True(response.Headers.CacheControl?.MustRevalidate);
            Assert.Equal(TimeSpan.FromHours(1), response.Headers.CacheControl?.MaxAge);
            Assert.Contains("User-agent: *", content);
            Assert.Contains("Allow: /", content);
            Assert.Contains("Disallow: /api/", content);
            Assert.Contains("Sitemap: https://shop.example.com/sitemap.xml", content);
        }

        [Fact]
        public async Task SitemapEndpoint_Returns503WithRetryAfter_WhenCatalogFeedIsUnavailable()
        {
            using var client = CreateClient(new GetPublicCatalogSitemap(), HttpStatusCode.ServiceUnavailable);

            using var response = await client.GetAsync("/sitemap.xml");

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("no-store, no-cache, max-age=0", response.Headers.CacheControl?.ToString());
            Assert.Equal(TimeSpan.FromMinutes(10), response.Headers.RetryAfter?.Delta);
        }

        private HttpClient CreateClient(GetPublicCatalogSitemap sitemapPayload, HttpStatusCode sitemapStatusCode = HttpStatusCode.OK)
        {
            var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<StorefrontApiClient>();
                    services.RemoveAll<IStorefrontSeoSettingsProvider>();

                    services.AddScoped(_ => new StorefrontApiClient(new HttpClient(new DiscoveryHttpMessageHandler(sitemapPayload, sitemapStatusCode))
                    {
                        BaseAddress = new Uri("https://api.example.com/api/"),
                    }));
                    services.AddScoped<IStorefrontSeoSettingsProvider>(_ => new StubSeoSettingsProvider(new SeoSettingsDto
                    {
                        SiteName = "BlazorShop",
                        DefaultTitleSuffix = "| BlazorShop",
                        DefaultMetaDescription = "Shop the published BlazorShop catalog.",
                        BaseCanonicalUrl = "https://shop.example.com",
                    }));
                });
            });

            return factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
        }

        private sealed class DiscoveryHttpMessageHandler : HttpMessageHandler
        {
            private readonly GetPublicCatalogSitemap _sitemapPayload;
            private readonly HttpStatusCode _sitemapStatusCode;

            public DiscoveryHttpMessageHandler(GetPublicCatalogSitemap sitemapPayload, HttpStatusCode sitemapStatusCode)
            {
                _sitemapPayload = sitemapPayload;
                _sitemapStatusCode = sitemapStatusCode;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.RequestUri?.AbsolutePath.EndsWith("/public/catalog/sitemap", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return Task.FromResult(new HttpResponseMessage(_sitemapStatusCode)
                    {
                        Content = _sitemapStatusCode == HttpStatusCode.OK ? JsonContent.Create(_sitemapPayload) : null,
                        RequestMessage = request,
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    RequestMessage = request,
                });
            }
        }

        private sealed class StubSeoSettingsProvider : IStorefrontSeoSettingsProvider
        {
            private readonly SeoSettingsDto _settings;

            public StubSeoSettingsProvider(SeoSettingsDto settings)
            {
                _settings = settings;
            }

            public Task<SeoSettingsDto?> GetAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult<SeoSettingsDto?>(_settings);
            }
        }
    }
}