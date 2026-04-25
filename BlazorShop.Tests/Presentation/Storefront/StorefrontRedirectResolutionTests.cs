namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;
    using System.Net.Http.Json;

    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Storefront.Services;
    using BlazorShop.Storefront.Services.Contracts;
    using BlazorShop.Web.Shared.Models.Seo;

    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    using Xunit;

    public class StorefrontRedirectResolutionTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public StorefrontRedirectResolutionTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task LegacyProductPath_ReturnsPermanentRedirect()
        {
            using var client = CreateClient();

            using var response = await client.GetAsync("/product/legacy-runner");

            Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
            Assert.Equal("/product/metro-runner", response.Headers.Location?.OriginalString);
        }

        [Fact]
        public async Task LegacyProductPath_DropsOriginalQueryStringOnRedirect()
        {
            using var client = CreateClient();

            using var response = await client.GetAsync("/product/legacy-runner?utm_source=newsletter");

            Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
            Assert.Equal("/product/metro-runner", response.Headers.Location?.OriginalString);
        }

        [Fact]
        public async Task LegacyCategoryPath_ReturnsPermanentRedirect()
        {
            using var client = CreateClient();

            using var response = await client.GetAsync("/category/legacy-sneakers");

            Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
            Assert.Equal("/category/sneakers", response.Headers.Location?.OriginalString);
        }

        [Fact]
        public async Task ManualPublicRedirect_IsResolvedBeforeRouteRendering()
        {
            using var client = CreateClient();

            using var response = await client.GetAsync("/legacy-sale");

            Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
            Assert.Equal("/todays-deals", response.Headers.Location?.OriginalString);
        }

        [Fact]
        public async Task MissingProductRoute_WithoutRedirect_ReturnsNotFound()
        {
            using var client = CreateClient();

            using var response = await client.GetAsync("/product/missing-product");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Null(response.Headers.Location);
            Assert.Contains("noindex, nofollow", string.Join(',', response.Headers.GetValues("X-Robots-Tag")));
            Assert.DoesNotContain("rel=\"canonical\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("property=\"og:title\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("property=\"og:description\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("property=\"og:image\"", content, StringComparison.Ordinal);
        }

        [Fact]
        public async Task UnknownRoute_UsesRouterFallbackAndReturnsNotFound()
        {
            using var client = CreateClient();

            using var response = await client.GetAsync("/missing-storefront-page");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Null(response.Headers.Location);
            Assert.Contains("noindex, nofollow", string.Join(',', response.Headers.GetValues("X-Robots-Tag")));
            Assert.DoesNotContain("rel=\"canonical\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("property=\"og:title\"", content, StringComparison.Ordinal);
        }

        private HttpClient CreateClient()
        {
            var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<StorefrontApiClient>();
                    services.RemoveAll<IStorefrontSeoSettingsProvider>();

                    services.AddScoped(_ => new StorefrontApiClient(new HttpClient(new RedirectHttpMessageHandler())
                    {
                        BaseAddress = new Uri("https://api.example.com/api/"),
                    }));
                    services.AddScoped<IStorefrontSeoSettingsProvider>(_ => new StubSeoSettingsProvider(new SeoSettingsDto
                    {
                        SiteName = "BlazorShop",
                        CompanyName = "BlazorShop",
                        DefaultTitleSuffix = "| BlazorShop",
                        DefaultMetaDescription = "Shop the published BlazorShop catalog.",
                        BaseCanonicalUrl = "https://shop.example.com",
                        CompanyLogoUrl = "/assets/logo.png",
                    }));
                });
            });

            return factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
        }

        private sealed class RedirectHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var path = request.RequestUri?.AbsolutePath ?? string.Empty;
                var query = request.RequestUri?.Query ?? string.Empty;

                if (path.EndsWith("/public/seo/redirects/resolve", StringComparison.OrdinalIgnoreCase))
                {
                    return ResolveRedirectAsync(query, request);
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    RequestMessage = request,
                });
            }

            private static Task<HttpResponseMessage> ResolveRedirectAsync(string query, HttpRequestMessage request)
            {
                var decodedQuery = Uri.UnescapeDataString(query);

                SeoRedirectResolutionDto? payload = decodedQuery switch
                {
                    var value when value.Contains("/product/legacy-runner", StringComparison.Ordinal) => new SeoRedirectResolutionDto
                    {
                        NewPath = "/product/metro-runner",
                        StatusCode = 301,
                    },
                    var value when value.Contains("/category/legacy-sneakers", StringComparison.Ordinal) => new SeoRedirectResolutionDto
                    {
                        NewPath = "/category/sneakers",
                        StatusCode = 301,
                    },
                    var value when value.Contains("/legacy-sale", StringComparison.Ordinal) => new SeoRedirectResolutionDto
                    {
                        NewPath = "/todays-deals",
                        StatusCode = 301,
                    },
                    _ => null,
                };

                return Task.FromResult(payload is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        RequestMessage = request,
                    }
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(payload),
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