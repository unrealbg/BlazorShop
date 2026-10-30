namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;
    using System.Text.Json;

    using BlazorShop.Storefront.Services;
    using BlazorShop.Storefront.Services.Contracts;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;

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
                UnitPrice = 0.01m,
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
        public async Task CartRoute_EmptyCartRetainsPrivacyAndOnlyOffersShoppingNavigation()
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);
            using var response = await client.GetAsync(StorefrontRoutes.Cart);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store, no-cache, max-age=0", response.Headers.CacheControl?.ToString());
            Assert.Contains("noindex,nofollow", html, StringComparison.Ordinal);
            Assert.Contains("Your cart is empty", html, StringComparison.Ordinal);
            Assert.Contains("0 items in cart", html, StringComparison.Ordinal);
            Assert.Contains("EUR 0.00", html, StringComparison.Ordinal);
            Assert.Contains("href=\"/new-releases\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("Continue to Checkout", html, StringComparison.Ordinal);
            Assert.DoesNotContain("data-storefront-cart-clear", html, StringComparison.Ordinal);
            Assert.DoesNotContain("rel=\"canonical\"", html, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CartRoute_TwoVariantsKeepSeparateControlsMetadataAndCurrentPrices()
        {
            var product = new StorefrontSeoAuditScenario().Product;
            var firstId = Guid.NewGuid();
            var secondId = Guid.NewGuid();
            product.Image = null;
            product.Variants =
            [
                new GetProductVariant { Id = firstId, ProductId = product.Id, Sku = "RUN-10-BLUE", SizeValue = "10", Color = "Blue", Price = 10.25m, Stock = 10 },
                new GetProductVariant { Id = secondId, ProductId = product.Id, Sku = "RUN-11-GREEN", SizeValue = "11", Color = "Green", Price = 12.50m, Stock = 10 },
            ];
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory, new StorefrontSeoAuditScenario { Product = product });
            using var request = new HttpRequestMessage(HttpMethod.Get, StorefrontRoutes.Cart);
            request.Headers.Add("Cookie", CreateCartCookieHeader(
                new ProcessCart { ProductId = product.Id, VariantId = firstId, Quantity = 2, UnitPrice = 999m, SizeValue = "stale" },
                new ProcessCart { ProductId = product.Id, VariantId = secondId, Quantity = 3, UnitPrice = 999m, SizeValue = "stale" }));
            using var response = await client.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, html.Split("data-storefront-cart-quantity", StringSplitOptions.None).Length - 1);
            Assert.Equal(2, html.Split("data-storefront-cart-remove", StringSplitOptions.None).Length - 1);
            Assert.Contains($"data-variant-id=\"{firstId}\"", html, StringComparison.Ordinal);
            Assert.Contains($"data-variant-id=\"{secondId}\"", html, StringComparison.Ordinal);
            Assert.Contains("SKU RUN-10-BLUE", html, StringComparison.Ordinal);
            Assert.Contains("SKU RUN-11-GREEN", html, StringComparison.Ordinal);
            Assert.Contains("Size 10", html, StringComparison.Ordinal);
            Assert.Contains("Size 11", html, StringComparison.Ordinal);
            Assert.Contains("Blue", html, StringComparison.Ordinal);
            Assert.Contains("Green", html, StringComparison.Ordinal);
            Assert.Contains("EUR 20.50", html, StringComparison.Ordinal);
            Assert.Contains("EUR 37.50", html, StringComparison.Ordinal);
            Assert.Contains("EUR 58.00", html, StringComparison.Ordinal);
            Assert.Contains("5 items in cart", html, StringComparison.Ordinal);
            Assert.Contains("No image available", html, StringComparison.Ordinal);
            Assert.Contains("for=\"cart-quantity-0\"", html, StringComparison.Ordinal);
            Assert.Contains("for=\"cart-quantity-1\"", html, StringComparison.Ordinal);
            Assert.Contains("data-size-value=\"10\"", html, StringComparison.Ordinal);
            Assert.Contains("href=\"/checkout\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("999.00", html, StringComparison.Ordinal);
            Assert.DoesNotContain("stale", html, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("missing-product")]
        [InlineData("missing-variant")]
        [InlineData("zero-stock")]
        [InlineData("api-unavailable")]
        public async Task CartRoute_UnavailableStatesRetainRowsWarningsAndCheckoutPolicy(string state)
        {
            var product = new StorefrontSeoAuditScenario().Product;
            product.Quantity = state == "zero-stock" ? 0 : 5;
            var productId = state == "missing-product" ? Guid.NewGuid() : product.Id;
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory, new StorefrontSeoAuditScenario
            {
                Product = product,
                CartProductsStatusCode = state == "api-unavailable" ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK,
            });
            using var request = new HttpRequestMessage(HttpMethod.Get, StorefrontRoutes.Cart);
            request.Headers.Add("Cookie", CreateCartCookieHeader(new ProcessCart
            {
                ProductId = productId,
                VariantId = state == "missing-variant" ? Guid.NewGuid() : null,
                Quantity = 2,
                UnitPrice = 999m,
            }));
            using var response = await client.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
            Assert.Contains("One cart item could not be refreshed", html, StringComparison.Ordinal);
            Assert.Contains("This item is unavailable or could not be refreshed", html, StringComparison.Ordinal);
            Assert.Contains("data-storefront-cart-remove", html, StringComparison.Ordinal);
            Assert.Contains("data-storefront-cart-quantity", html, StringComparison.Ordinal);
            Assert.Contains("Continue to Checkout", html, StringComparison.Ordinal);
            Assert.Contains("2 items in cart", html, StringComparison.Ordinal);
            Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
            if (state is "missing-product" or "api-unavailable")
            {
                Assert.Contains("Unavailable item", html, StringComparison.Ordinal);
                Assert.Contains("EUR 0.00", html, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains("EUR 259.90", html, StringComparison.Ordinal);
            }
        }

        [Fact]
        public async Task CartRoute_PartialCatalogFailurePreservesAvailableAndUnavailableLines()
        {
            var product = new StorefrontSeoAuditScenario().Product;
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory, new StorefrontSeoAuditScenario
            {
                CartProducts = new Dictionary<Guid, GetProduct> { [product.Id] = product },
            });
            using var request = new HttpRequestMessage(HttpMethod.Get, StorefrontRoutes.Cart);
            request.Headers.Add("Cookie", CreateCartCookieHeader(
                new ProcessCart { ProductId = product.Id, Quantity = 1 },
                new ProcessCart { ProductId = Guid.NewGuid(), Quantity = 1 }));
            using var response = await client.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Metro Runner", html, StringComparison.Ordinal);
            Assert.Contains("Unavailable item", html, StringComparison.Ordinal);
            Assert.Contains("EUR 129.95", html, StringComparison.Ordinal);
            Assert.Contains("EUR 0.00", html, StringComparison.Ordinal);
            Assert.Equal(2, html.Split("data-storefront-cart-remove", StringSplitOptions.None).Length - 1);
            Assert.Contains("One cart item could not be refreshed", html, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CartRoute_MalformedCookieShowsRecoveryWithoutCheckoutOrCookieRewrite()
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);
            using var request = new HttpRequestMessage(HttpMethod.Get, StorefrontRoutes.Cart);
            request.Headers.Add("Cookie", "my-cart=%7Bbroken-json");
            using var response = await client.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("We couldn&#x27;t read the saved cart cookie", html, StringComparison.Ordinal);
            Assert.Contains("bs-v2-cart-error", html, StringComparison.Ordinal);
            Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
            Assert.Contains("Your cart is empty", html, StringComparison.Ordinal);
            Assert.DoesNotContain("Continue to Checkout", html, StringComparison.Ordinal);
            Assert.False(response.Headers.Contains("Set-Cookie"));
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

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public async Task ShellAccountLinks_ReflectSessionWithoutDuplicateRefresh(bool authenticated, bool admin)
        {
            var resolver = new StubStorefrontSessionResolver(new StorefrontSessionInfo(authenticated, admin, "Customer", "customer@example.com"));
            using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStorefrontSessionResolver>();
                services.RemoveAll<IStorefrontClientAppUrlResolver>();
                services.AddScoped<IStorefrontSessionResolver>(_ => resolver);
                services.AddScoped<IStorefrontClientAppUrlResolver>(_ => new StubStorefrontClientAppUrlResolver("https://account.example.com/"));
            }));
            using var client = StorefrontSeoAuditClientFactory.CreateClient(factory);
            using var response = await client.GetAsync(StorefrontRoutes.Home);
            var html = await response.Content.ReadAsStringAsync();
            var footer = html[html.IndexOf("<footer", StringComparison.Ordinal)..html.IndexOf("</footer>", StringComparison.Ordinal)];

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, resolver.Calls);
            if (authenticated)
            {
                Assert.Contains("href=\"https://account.example.com/account\">My Account", footer);
                Assert.DoesNotContain("href=\"/signin\"", footer);
                Assert.DoesNotContain("href=\"/register\"", footer);
                Assert.Equal(admin, footer.Contains("href=\"https://account.example.com/admin\">Admin Panel", StringComparison.Ordinal));
            }
            else
            {
                Assert.Contains("href=\"/signin\">Sign in", footer);
                Assert.Contains("href=\"/register\">Register", footer);
                Assert.DoesNotContain("My Account", footer);
                Assert.DoesNotContain("Admin Panel", footer);
            }
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

        [Theory]
        [InlineData("[null]")]
        [InlineData("[null,{\"ProductId\":\"22222222-2222-2222-2222-222222222222\",\"Quantity\":2}]")]
        public async Task CartRoute_NullCookieEntriesDoNotReturn500(string json)
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);
            using var request = new HttpRequestMessage(HttpMethod.Get, StorefrontRoutes.Cart);
            request.Headers.Add("Cookie", $"my-cart={Uri.EscapeDataString(json)}");
            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [MemberData(nameof(BlazorShop.Tests.Presentation.Payments.CartCookieParserTests.Cases), MemberType = typeof(BlazorShop.Tests.Presentation.Payments.CartCookieParserTests))]
        public async Task CartRoute_InvalidCookieMatrixPreservesSafeRowsAndPrivacy(string name, string json, int items, long quantity, bool invalid)
        {
            Assert.NotEmpty(name);
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory);
            using var request = new HttpRequestMessage(HttpMethod.Get, StorefrontRoutes.Cart);
            request.Headers.Add("Cookie", $"my-cart={Uri.EscapeDataString(json)}");
            using var response = await client.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-store, no-cache, max-age=0", response.Headers.CacheControl?.ToString());
            Assert.Contains("noindex,nofollow", html, StringComparison.Ordinal);
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.Equal(items, html.Split("data-storefront-cart-remove", StringSplitOptions.None).Length - 1);
            Assert.Contains($"{quantity} item", html, StringComparison.Ordinal);
            if (invalid)
            {
                Assert.Contains("role=\"alert\"", html, StringComparison.Ordinal);
            }
        }

        [Fact]
        public async Task CartRoute_UnrepresentableApiTotalRendersUnavailableInsteadOf500()
        {
            var product = new StorefrontSeoAuditScenario().Product;
            product.Price = decimal.MaxValue;
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory, new StorefrontSeoAuditScenario { Product = product });
            using var request = new HttpRequestMessage(HttpMethod.Get, StorefrontRoutes.Cart);
            request.Headers.Add("Cookie", CreateCartCookieHeader(new ProcessCart { ProductId = product.Id, Quantity = int.MaxValue, UnitPrice = 0.01m }));
            using var response = await client.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("totals cannot be displayed safely", html, StringComparison.Ordinal);
            Assert.Contains("<dd>Unavailable</dd>", html, StringComparison.Ordinal);
            Assert.Contains("value=\"2147483647\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("EUR 0.01", html, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CartRoute_ConnectionInterruptionWithNullEntryRetainsUnavailableRow()
        {
            using var client = StorefrontSeoAuditClientFactory.CreateClient(_factory, new StorefrontSeoAuditScenario { CartConnectionInterrupted = true });
            using var request = new HttpRequestMessage(HttpMethod.Get, StorefrontRoutes.Cart);
            request.Headers.Add("Cookie", $"my-cart={Uri.EscapeDataString("[null,{\"ProductId\":\"22222222-2222-2222-2222-222222222222\",\"Quantity\":2}]")}");
            using var response = await client.SendAsync(request);
            var html = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Unavailable item", html, StringComparison.Ordinal);
            Assert.Contains("2 items in cart", html, StringComparison.Ordinal);
            Assert.False(response.Headers.Contains("Set-Cookie"));
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

            public int Calls { get; private set; }

            public StubStorefrontSessionResolver(StorefrontSessionInfo sessionInfo)
            {
                _sessionInfo = sessionInfo;
            }

            public Task<StorefrontSessionInfo> GetCurrentUserAsync(CancellationToken cancellationToken = default)
            {
                Calls++;
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

                if (TryCreateHttpAbsoluteUri(relativeOrAbsoluteUrl, out var absoluteUri))
                {
                    return absoluteUri.ToString();
                }

                var relativePath = relativeOrAbsoluteUrl.StartsWith("/", StringComparison.Ordinal)
                    ? relativeOrAbsoluteUrl
                    : $"/{relativeOrAbsoluteUrl}";

                return new Uri(new Uri(_baseUrl, UriKind.Absolute), relativePath).ToString();
            }

            private static bool TryCreateHttpAbsoluteUri(string value, out Uri uri)
            {
                return Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri!)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            }
        }
    }
}
