namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;
    using System.Text.Json;

    using BlazorShop.Storefront.Services;
    using BlazorShop.Storefront.Services.Contracts;
    using BlazorShop.Web.Shared.Models.Payment;

    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    using Xunit;

    public class StorefrontCartFlowTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private static readonly Guid MetroRunnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private readonly WebApplicationFactory<Program> _factory;

        public StorefrontCartFlowTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task CartRoute_Returns200AndRendersCookieBackedCartState()
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);
            using var request = new HttpRequestMessage(HttpMethod.Get, StorefrontRoutes.Cart);
            request.Headers.Add("Cookie", CreateCartCookieHeader(new ProcessCart
            {
                ProductId = MetroRunnerId,
                Quantity = 2,
                UnitPrice = 129.95m,
            }));

            using var response = await client.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store, no-cache, max-age=0", response.Headers.CacheControl?.ToString());
            Assert.True(response.Headers.TryGetValues("X-Robots-Tag", out var robotsHeaderValues));
            Assert.Contains("noindex, nofollow", robotsHeaderValues);
            Assert.DoesNotContain("rel=\"canonical\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("property=\"og:title\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("application/ld+json", content, StringComparison.Ordinal);
            Assert.Contains("My Cart", content, StringComparison.Ordinal);
            Assert.Contains("Metro Runner", content, StringComparison.Ordinal);
            Assert.Contains("value=\"2\"", content, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("259.90", content, StringComparison.Ordinal);
            Assert.Contains("href=\"/checkout\"", content, StringComparison.Ordinal);
            Assert.Contains("data-storefront-cart-remove", content, StringComparison.Ordinal);
            Assert.Contains("data-storefront-cart-quantity", content, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CheckoutRoute_DirectNavigation_RedirectsInsteadOfReturning503()
        {
            using var client = CreateCheckoutClient(StorefrontSessionInfo.Anonymous);

            using var response = await client.GetAsync(StorefrontRoutes.Checkout);

            Assert.NotEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("no-store, no-cache, max-age=0", response.Headers.CacheControl?.ToString());
            Assert.True(response.Headers.TryGetValues("X-Robots-Tag", out var robotsHeaderValues));
            Assert.Contains("noindex, nofollow", robotsHeaderValues);
        }

        [Fact]
        public async Task CheckoutRoute_AnonymousUser_RedirectsToClientAppSignInCheckout()
        {
            using var client = CreateCheckoutClient(StorefrontSessionInfo.Anonymous);

            using var response = await client.GetAsync(StorefrontRoutes.Checkout);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("https://account.example.com/authentication/login/account/checkout", response.Headers.Location?.ToString());
        }

        [Fact]
        public async Task CheckoutRoute_AuthenticatedCustomer_RedirectsToClientAppCheckout()
        {
            using var client = CreateCheckoutClient(new StorefrontSessionInfo(
                IsAuthenticated: true,
                IsAdmin: false,
                DisplayName: "Ada Customer",
                Email: "ada@example.com"));

            using var response = await client.GetAsync(StorefrontRoutes.Checkout);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("https://account.example.com/account/checkout", response.Headers.Location?.ToString());
        }

        [Fact]
        public async Task HeaderCartLink_TargetsLocalStorefrontCartRoute_AndMountsToastRegion()
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);

            using var response = await client.GetAsync(StorefrontRoutes.Home);
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("href=\"/my-cart\"", content, StringComparison.Ordinal);
            Assert.Contains("data-storefront-toast-region", content, StringComparison.Ordinal);
            Assert.Contains("data-storefront-toast-template", content, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CommerceScript_RegistersToastAndCartPageSupport()
        {
            using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

            using var response = await client.GetAsync("/js/storefrontCommerce.js");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("data-storefront-toast-region", content, StringComparison.Ordinal);
            Assert.Contains("data-storefront-cart-remove", content, StringComparison.Ordinal);
            Assert.Contains("data-storefront-cart-quantity", content, StringComparison.Ordinal);
            Assert.Contains("sessionStorage", content, StringComparison.Ordinal);
        }

        private static string CreateCartCookieHeader(params ProcessCart[] carts)
        {
            var cartJson = JsonSerializer.Serialize(carts);
            return $"my-cart={Uri.EscapeDataString(cartJson)}";
        }

        private HttpClient CreateCheckoutClient(StorefrontSessionInfo sessionInfo, string clientAppBaseUrl = "https://account.example.com/")
        {
            var configuredFactory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IStorefrontSessionResolver>();
                    services.RemoveAll<IStorefrontClientAppUrlResolver>();

                    services.AddScoped<IStorefrontSessionResolver>(_ => new StubStorefrontSessionResolver(sessionInfo));
                    services.AddScoped<IStorefrontClientAppUrlResolver>(_ => new StubStorefrontClientAppUrlResolver(clientAppBaseUrl));
                });
            });

            return StorefrontSeoAuditClientFactory.CreateClient(configuredFactory);
        }

        private sealed class StubStorefrontSessionResolver : IStorefrontSessionResolver
        {
            private readonly StorefrontSessionInfo _sessionInfo;

            public StubStorefrontSessionResolver(StorefrontSessionInfo sessionInfo)
            {
                _sessionInfo = sessionInfo;
            }

            public Task<StorefrontSessionInfo> GetCurrentUserAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(_sessionInfo);
            }
        }

        private sealed class StubStorefrontClientAppUrlResolver : IStorefrontClientAppUrlResolver
        {
            private readonly string _baseUrl;

            public StubStorefrontClientAppUrlResolver(string baseUrl)
            {
                _baseUrl = baseUrl.EndsWith("/", StringComparison.Ordinal)
                    ? baseUrl
                    : $"{baseUrl}/";
            }

            public string? ResolveBaseUrl()
            {
                return _baseUrl;
            }

            public string ResolveUrl(string? relativeOrAbsoluteUrl)
            {
                if (string.IsNullOrWhiteSpace(relativeOrAbsoluteUrl))
                {
                    return _baseUrl;
                }

                if (Uri.TryCreate(relativeOrAbsoluteUrl.Trim(), UriKind.Absolute, out var absoluteUri))
                {
                    return absoluteUri.ToString();
                }

                var relativePath = relativeOrAbsoluteUrl.StartsWith("/", StringComparison.Ordinal)
                    ? relativeOrAbsoluteUrl
                    : $"/{relativeOrAbsoluteUrl}";

                return new Uri(new Uri(_baseUrl, UriKind.Absolute), relativePath).ToString();
            }
        }
    }
}