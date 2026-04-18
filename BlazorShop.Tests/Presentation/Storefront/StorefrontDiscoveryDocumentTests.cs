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
            Assert.Contains("User-agent: *", content);
            Assert.Contains("Allow: /", content);
            Assert.Contains("Disallow: /api/", content);
            Assert.Contains("Sitemap: https://shop.example.com/sitemap.xml", content);
        }

        private HttpClient CreateClient(GetPublicCatalogSitemap sitemapPayload)
        {
            var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<StorefrontApiClient>();
                    services.RemoveAll<IStorefrontSeoSettingsProvider>();

                    services.AddScoped(_ => new StorefrontApiClient(new HttpClient(new DiscoveryHttpMessageHandler(sitemapPayload))
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

            public DiscoveryHttpMessageHandler(GetPublicCatalogSitemap sitemapPayload)
            {
                _sitemapPayload = sitemapPayload;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.RequestUri?.AbsolutePath.EndsWith("/public/catalog/sitemap", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(_sitemapPayload),
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