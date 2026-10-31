namespace BlazorShop.Tests.Presentation.Authentication
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Security.Cryptography;

    using BlazorShop.API.SessionObservation;
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.Options;
    using BlazorShop.Storefront.Services;
    using BlazorShop.Storefront.Services.Contracts;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Moq;
    using Xunit;

    public sealed class SessionObservationTransportTests
    {
        [Theory]
        [InlineData("Production", false, false)]
        [InlineData("Production", true, false)]
        [InlineData("Development", false, false)]
        [InlineData("Development", true, true)]
        public async Task HttpTransport_IsOnlyAvailableWithExplicitDevelopmentOptIn(string environmentName, bool optIn, bool allowed)
        {
            var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environmentName });
            builder.Logging.ClearProviders();
            builder.WebHost.UseTestServer();
            builder.Services.Configure<SessionObservationOptions>(options =>
            {
                options.Enabled = true;
                options.ServiceKey = key;
                options.AllowInsecureDevelopmentTransport = optIn;
            });
            await using var app = builder.Build();
            app.UseMiddleware<SessionObservationGate>();
            app.MapGet("/observe", () => Results.NoContent()).WithMetadata(new SessionObservationEndpointAttribute());
            await app.StartAsync();
            using var client = app.GetTestClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost/observe");
            request.Headers.Add(SessionObservationOptions.CredentialHeader, key);
            request.Headers.Add("X-Forwarded-Proto", "https");
            request.Headers.Add("X-Forwarded-For", "127.0.0.1");
            using var response = await client.SendAsync(request);
            Assert.Equal(allowed ? HttpStatusCode.NoContent : HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore);
        }

        [Theory]
        [InlineData("Production", false)]
        [InlineData("Production", true)]
        [InlineData("Development", false)]
        public async Task ClientDoesNotSendCredentialOverDisallowedTransport(string environmentName, bool optIn)
        {
            var environment = Mock.Of<IHostEnvironment>(host => host.EnvironmentName == environmentName);
            var destination = new ResponseHandler(() => new HttpResponseMessage(HttpStatusCode.OK));
            using var handler = new SessionObservationCredentialHandler(Options.Create(new SessionObservationOptions
            {
                Enabled = true,
                ServiceKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                AllowInsecureDevelopmentTransport = optIn,
            }), environment) { InnerHandler = destination };
            using var client = new HttpClient(handler);
            await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("http://api.example.test/api/internal/storefront/session"));
            Assert.Equal(0, destination.Calls);
        }

        [Fact]
        public async Task PooledTransportsDisableCookieJarsAndRedirects_ObserverHasNoRetryHandler()
        {
            await using var app = new WebApplicationFactory<global::Program>();
            var factory = app.Services.GetRequiredService<IHttpMessageHandlerFactory>();
            foreach (var name in new[] { nameof(IStorefrontSessionResolver), nameof(StorefrontApiClient) })
            {
                var types = new List<string>();
                var handler = factory.CreateHandler(name);
                while (handler is DelegatingHandler delegating)
                {
                    types.Add(handler.GetType().Name);
                    handler = delegating.InnerHandler!;
                }
                var transport = Assert.IsType<SocketsHttpHandler>(handler);
                Assert.False(transport.UseCookies);
                Assert.False(transport.AllowAutoRedirect);
                if (name == nameof(IStorefrontSessionResolver)) Assert.DoesNotContain(types, type => type.Contains("Resilience", StringComparison.Ordinal));
            }
        }

        [Theory]
        [InlineData("network")]
        [InlineData("invalid-json")]
        [InlineData("server-error")]
        [InlineData("redirect")]
        [InlineData("timeout")]
        public async Task ObservationFailure_IsAnonymousWithNoCookieForwardingOrSensitiveResponse(string failure)
        {
            var destination = new ResponseHandler(() => failure switch
            {
                "network" => throw new HttpRequestException("sensitive upstream diagnostic"),
                "timeout" => throw new TaskCanceledException("sensitive upstream diagnostic"),
                "invalid-json" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("sensitive upstream diagnostic") },
                "redirect" => new HttpResponseMessage(HttpStatusCode.Redirect),
                _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            });
            var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            await using var app = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:BaseUrl"] = "https://api.example.test/api/",
                    ["SessionObservation:Enabled"] = "true",
                    ["SessionObservation:ServiceKey"] = key,
                }));
                builder.ConfigureTestServices(services => services.AddHttpClient<IStorefrontSessionResolver, StorefrontSessionResolver>()
                    .ConfigurePrimaryHttpMessageHandler(() => destination));
            });
            var context = new DefaultHttpContext();
            context.Request.Headers.Cookie = "__Host-blazorshop-refresh=isolated-test-credential";
            app.Services.GetRequiredService<IHttpContextAccessor>().HttpContext = context;
            await using var scope = app.Services.CreateAsyncScope();
            var state = await scope.ServiceProvider.GetRequiredService<IStorefrontSessionResolver>().GetCurrentUserAsync();
            Assert.Equal(StorefrontSessionInfo.Anonymous, state);
            Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
            Assert.Equal(1, destination.Calls);
        }

        [Fact]
        public async Task UpstreamSetCookieIsNeverForwarded()
        {
            var destination = new ResponseHandler(() =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new SessionObservation(true, false, "User", "user@example.test")) };
                response.Headers.Add("Set-Cookie", "__Host-blazorshop-refresh=must-not-forward; Secure; HttpOnly; Path=/");
                return response;
            });
            var context = new DefaultHttpContext();
            context.Request.Headers.Cookie = "__Host-blazorshop-refresh=isolated-test-credential";
            using var client = new HttpClient(destination) { BaseAddress = new Uri("https://api.example.test/api/") };
            var resolver = new StorefrontSessionResolver(client, new HttpContextAccessor { HttpContext = context }, new ConfigurationBuilder().Build(),
                Options.Create(new SessionObservationOptions { Enabled = true }));
            var state = await resolver.GetCurrentUserAsync();
            Assert.True(state.IsAuthenticated);
            Assert.False(context.Response.Headers.ContainsKey("Set-Cookie"));
        }

        [Fact]
        public async Task CatalogClient_DoesNotRetryCredentialBearingUnsafeMethods()
        {
            var destination = new ResponseHandler(() => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            await using var app = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:BaseUrl"] = "https://api.example.test/api/",
                }));
                builder.ConfigureTestServices(services => services.AddHttpClient<StorefrontApiClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => destination));
            });
            using var client = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(StorefrontApiClient));
            using var response = await client.PostAsync("internal/unsafe-test", new StringContent("isolated test"));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(1, destination.Calls);
        }

        private sealed class ResponseHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
        {
            public int Calls { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Calls++;
                return Task.FromResult(respond());
            }
        }
    }
}
