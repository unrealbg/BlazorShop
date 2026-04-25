namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;

    using BlazorShop.Application.Diagnostics;
    using BlazorShop.Storefront.Services;
    using BlazorShop.Storefront.Services.Contracts;
    using BlazorShop.Tests.Support.Logging;

    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;
    using Microsoft.Extensions.Logging;

    using Xunit;

    public class StorefrontDiscoveryMonitoringTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public StorefrontDiscoveryMonitoringTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task SitemapFailure_EmitsDiscoveryFailureEvent()
        {
            var sink = new TestLogSink();
            using var client = CreateClient(
                sink,
                new StubSitemapService(StorefrontSitemapGenerationResult.ServiceUnavailable()),
                new StubRobotsService("User-agent: *\nAllow: /\n"));

            using var response = await client.GetAsync(StorefrontRoutes.Sitemap);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Contains(sink.Entries, entry =>
                entry.EventId.Id == 7011
                && entry.LogLevel == LogLevel.Warning
                && entry.GetString("SeoEvent") == SeoRuntimeEventNames.PublicDiscoverySitemapFailure
                && entry.GetString("DocumentPath") == StorefrontRoutes.Sitemap
                && entry.GetString("FailureReason") == "upstream_service_unavailable");
        }

        [Fact]
        public async Task RobotsException_EmitsDiscoveryFailureEvent()
        {
            var sink = new TestLogSink();
            using var client = CreateClient(
                sink,
                new StubSitemapService(StorefrontSitemapGenerationResult.Success("<urlset />")),
                new ThrowingRobotsService());

            using var response = await client.GetAsync(StorefrontRoutes.Robots);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Contains(sink.Entries, entry =>
                entry.EventId.Id == 7014
                && entry.LogLevel == LogLevel.Error
                && entry.GetString("SeoEvent") == SeoRuntimeEventNames.PublicDiscoveryRobotsFailure
                && entry.GetString("DocumentPath") == StorefrontRoutes.Robots
                && entry.GetString("FailureReason") == "generation_exception");
        }

        private HttpClient CreateClient(TestLogSink sink, IStorefrontSitemapService sitemapService, IStorefrontRobotsService robotsService)
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
                    services.RemoveAll<IStorefrontSitemapService>();
                    services.RemoveAll<IStorefrontRobotsService>();

                    services.AddScoped(_ => sitemapService);
                    services.AddScoped(_ => robotsService);
                });
            });

            return factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
        }

        private sealed class StubSitemapService : IStorefrontSitemapService
        {
            private readonly StorefrontSitemapGenerationResult _result;

            public StubSitemapService(StorefrontSitemapGenerationResult result)
            {
                _result = result;
            }

            public Task<StorefrontSitemapGenerationResult> GenerateAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_result);
            }
        }

        private sealed class StubRobotsService : IStorefrontRobotsService
        {
            private readonly string _content;

            public StubRobotsService(string content)
            {
                _content = content;
            }

            public Task<string> GenerateAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_content);
            }
        }

        private sealed class ThrowingRobotsService : IStorefrontRobotsService
        {
            public Task<string> GenerateAsync(CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("robots generation failed");
            }
        }
    }
}