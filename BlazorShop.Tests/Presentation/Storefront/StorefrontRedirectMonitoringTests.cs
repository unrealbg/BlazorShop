namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;
    using System.Net.Http.Json;

    using BlazorShop.Application.Diagnostics;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Storefront.Services;
    using BlazorShop.Storefront.Services.Contracts;
    using BlazorShop.Tests.Support.Logging;
    using BlazorShop.Web.Shared.Models.Seo;

    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;

    using Xunit;

    public class StorefrontRedirectMonitoringTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public StorefrontRedirectMonitoringTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task RedirectHit_EmitsResolvedSeoRuntimeEvent()
        {
            var sink = new TestLogSink();
            using var client = CreateClient(sink, path => path == "/product/legacy-runner"
                ? new SeoRedirectResolutionDto { NewPath = "/product/metro-runner", StatusCode = 301 }
                : null);

            using var response = await client.GetAsync("/product/legacy-runner");

            Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
            Assert.Equal("/product/metro-runner", response.Headers.Location?.OriginalString);
            Assert.Contains(sink.Entries, entry =>
                entry.EventId.Id == 7007
                && entry.LogLevel == LogLevel.Information
                && entry.GetString("SeoEvent") == SeoRuntimeEventNames.PublicRedirectResolved
                && entry.GetString("SourcePath") == "/product/legacy-runner"
                && entry.GetString("DestinationPath") == "/product/metro-runner"
                && entry.GetInt32("StatusCode") == 301);
        }

        [Fact]
        public async Task SelfLoopRedirect_IsBlockedAndLogged()
        {
            var sink = new TestLogSink();
            using var client = CreateClient(sink, path => path == "/product/legacy-runner"
                ? new SeoRedirectResolutionDto { NewPath = "/product/legacy-runner", StatusCode = 301 }
                : null);

            using var response = await client.GetAsync("/product/legacy-runner");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Null(response.Headers.Location);
            Assert.Contains(sink.Entries, entry =>
                entry.EventId.Id == 7008
                && entry.LogLevel == LogLevel.Warning
                && entry.GetString("SeoEvent") == SeoRuntimeEventNames.PublicRedirectLoopBlocked
                && entry.GetString("SourcePath") == "/product/legacy-runner");
        }

        [Fact]
        public async Task InvalidRedirectTarget_IsBlockedAndLogged()
        {
            var sink = new TestLogSink();
            using var client = CreateClient(sink, path => path == "/legacy-sale"
                ? new SeoRedirectResolutionDto { NewPath = "https://bad.example.com/offsite", StatusCode = 301 }
                : null);

            using var response = await client.GetAsync("/legacy-sale");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Null(response.Headers.Location);
            Assert.Contains(sink.Entries, entry =>
                entry.EventId.Id == 7010
                && entry.LogLevel == LogLevel.Warning
                && entry.GetString("SeoEvent") == SeoRuntimeEventNames.PublicRedirectInvalidTargetBlocked
                && entry.GetString("SourcePath") == "/legacy-sale"
                && entry.GetString("TargetPath") == "https://bad.example.com/offsite");
        }

        private HttpClient CreateClient(TestLogSink sink, Func<string, SeoRedirectResolutionDto?> redirectResolver)
        {
            var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddLogging(logging =>
                    {
                        logging.ClearProviders();
                        logging.AddProvider(sink.CreateProvider());
                    });
                    services.RemoveAll<StorefrontApiClient>();
                    services.RemoveAll<IStorefrontSeoSettingsProvider>();

                    services.AddScoped(_ => new StorefrontApiClient(new HttpClient(new RedirectMonitoringHttpMessageHandler(redirectResolver))
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

        private sealed class RedirectMonitoringHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<string, SeoRedirectResolutionDto?> _redirectResolver;

            public RedirectMonitoringHttpMessageHandler(Func<string, SeoRedirectResolutionDto?> redirectResolver)
            {
                _redirectResolver = redirectResolver;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var path = request.RequestUri?.AbsolutePath ?? string.Empty;
                if (!path.EndsWith("/public/seo/redirects/resolve", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        RequestMessage = request,
                    });
                }

                var decodedQuery = Uri.UnescapeDataString(request.RequestUri?.Query ?? string.Empty);
                var requestPath = decodedQuery.StartsWith("?path=", StringComparison.OrdinalIgnoreCase)
                    ? decodedQuery[6..]
                    : decodedQuery;
                var payload = _redirectResolver(requestPath);

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