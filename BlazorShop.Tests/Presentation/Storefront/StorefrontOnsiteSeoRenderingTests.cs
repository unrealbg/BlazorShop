namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;
    using System.Net.Http.Json;

    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Storefront.Services;
    using BlazorShop.Storefront.Services.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Product;

    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    using Xunit;

    public class StorefrontOnsiteSeoRenderingTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private static readonly Guid SneakersCategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid MetroRunnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid TrailRunnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        private readonly WebApplicationFactory<Program> _factory;

        public StorefrontOnsiteSeoRenderingTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task CategoryPage_RendersSeoContentBreadcrumbsAndProductLinks_WhenSeoContentExists()
        {
            using var client = CreateClient();

            using var response = await client.GetAsync("/category/sneakers");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("aria-label=\"Breadcrumb\"", content, StringComparison.Ordinal);
            Assert.Contains("aria-current=\"page\"", content, StringComparison.Ordinal);
            Assert.Contains(">Sneakers</span>", content, StringComparison.Ordinal);
            Assert.Contains("href=\"/product/metro-runner\"", content, StringComparison.Ordinal);
            Assert.Contains("href=\"/product/trail-runner\"", content, StringComparison.Ordinal);
            Assert.Contains("Category Guide", content, StringComparison.Ordinal);
            Assert.Contains("Designed for daily mileage and weekend recovery walks.", content, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CategoryPage_OmitsSeoContentBlock_WhenSeoContentIsMissing()
        {
            using var client = CreateClient(new StorefrontStubOptions(CategorySeoContent: null));

            using var response = await client.GetAsync("/category/sneakers");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("Category Guide", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Designed for daily mileage and weekend recovery walks.", content, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ProductPage_RendersSeoContentBreadcrumbsAndRelatedCategoryLinks_WhenDataExists()
        {
            using var client = CreateClient();

            using var response = await client.GetAsync("/product/metro-runner");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("aria-label=\"Breadcrumb\"", content, StringComparison.Ordinal);
            Assert.Contains("aria-current=\"page\"", content, StringComparison.Ordinal);
            Assert.Contains(">Metro Runner</span>", content, StringComparison.Ordinal);
            Assert.Contains("href=\"/category/sneakers\"", content, StringComparison.Ordinal);
            Assert.Contains("Back to Sneakers", content, StringComparison.Ordinal);
            Assert.Contains("More from Sneakers", content, StringComparison.Ordinal);
            Assert.Contains("href=\"/product/trail-runner\"", content, StringComparison.Ordinal);
            Assert.Contains("Product Details", content, StringComparison.Ordinal);
            Assert.Contains("Metro Runner adds breathable mesh and responsive cushioning.", content, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ProductPage_OmitsSeoContentBlock_WhenSeoContentIsMissing_ButKeepsDiscoveryLinks()
        {
            using var client = CreateClient(new StorefrontStubOptions(ProductSeoContent: null, IncludeRelatedProducts: false));

            using var response = await client.GetAsync("/product/metro-runner");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("Product Details", content, StringComparison.Ordinal);
            Assert.DoesNotContain("Metro Runner adds breathable mesh and responsive cushioning.", content, StringComparison.Ordinal);
            Assert.Contains("href=\"/category/sneakers\"", content, StringComparison.Ordinal);
            Assert.Contains("href=\"/new-releases\"", content, StringComparison.Ordinal);
            Assert.Contains("href=\"/todays-deals\"", content, StringComparison.Ordinal);
            Assert.Contains("Keep browsing the public catalog", content, StringComparison.Ordinal);
        }

        [Fact]
        public async Task HomePage_RendersCrawlableCategoryAndProductLinks()
        {
            using var client = CreateClient();

            using var response = await client.GetAsync("/");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("href=\"/category/sneakers\"", content, StringComparison.Ordinal);
            Assert.Contains("href=\"/product/metro-runner\"", content, StringComparison.Ordinal);
            Assert.Contains("href=\"/new-releases\"", content, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("/new-releases")]
        [InlineData("/todays-deals")]
        public async Task ListingPages_RenderRealProductAndCategorySlugLinks(string path)
        {
            using var client = CreateClient();

            using var response = await client.GetAsync(path);
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("href=\"/product/metro-runner\"", content, StringComparison.Ordinal);
            Assert.Contains("href=\"/category/sneakers\"", content, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/new-releases")]
        [InlineData("/todays-deals")]
        public async Task ListingPages_WhenCatalogApiIsUnavailable_Return503WithoutCanonicalOrStructuredData(string path)
        {
            using var client = CreateClient(new StorefrontStubOptions(CatalogServiceUnavailable: true));

            using var response = await client.GetAsync(path);
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Contains("noindex, nofollow", string.Join(',', response.Headers.GetValues("X-Robots-Tag")));
            Assert.DoesNotContain("rel=\"canonical\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("property=\"og:title\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("property=\"og:description\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("property=\"og:image\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("\"@type\":\"CollectionPage\"", content, StringComparison.Ordinal);
            Assert.DoesNotContain("\"@type\":\"WebSite\"", content, StringComparison.Ordinal);
        }

        private HttpClient CreateClient(StorefrontStubOptions? options = null)
        {
            options ??= new StorefrontStubOptions();

            var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<StorefrontApiClient>();
                    services.RemoveAll<IStorefrontSeoSettingsProvider>();

                    services.AddScoped(_ => new StorefrontApiClient(new HttpClient(new OnsiteSeoHttpMessageHandler(options))
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

        private sealed record StorefrontStubOptions(string? CategorySeoContent = "Designed for daily mileage and weekend recovery walks.", string? ProductSeoContent = "Metro Runner adds breathable mesh and responsive cushioning.", bool IncludeRelatedProducts = true, bool CatalogServiceUnavailable = false);

        private sealed class OnsiteSeoHttpMessageHandler : HttpMessageHandler
        {
            private readonly StorefrontStubOptions _options;

            public OnsiteSeoHttpMessageHandler(StorefrontStubOptions options)
            {
                _options = options;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var path = request.RequestUri?.AbsolutePath ?? string.Empty;

                if (_options.CatalogServiceUnavailable && path.Contains("/public/catalog/", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    {
                        RequestMessage = request,
                    });
                }

                return path switch
                {
                    var value when value.EndsWith("/public/catalog/categories", StringComparison.OrdinalIgnoreCase) => JsonResponse<IReadOnlyList<GetCategory>>(
                    [
                        new GetCategory
                        {
                            Id = SneakersCategoryId,
                            Name = "Sneakers",
                            Slug = "sneakers",
                            MetaDescription = "Browse the latest sneakers.",
                            SeoContent = _options.CategorySeoContent,
                        },
                    ], request),
                    var value when value.EndsWith("/public/catalog/products", StringComparison.OrdinalIgnoreCase) => JsonResponse(new PagedResult<GetCatalogProduct>
                    {
                        Items = BuildListingProducts(),
                        PageNumber = 1,
                        PageSize = 24,
                        TotalCount = BuildListingProducts().Count,
                    }, request),
                    var value when value.EndsWith("/public/catalog/categories/slug/sneakers", StringComparison.OrdinalIgnoreCase) => JsonResponse(new GetCategoryPage
                    {
                        Category = new GetCategory
                        {
                            Id = SneakersCategoryId,
                            Name = "Sneakers",
                            Slug = "sneakers",
                            MetaDescription = "Browse the latest sneakers.",
                            SeoContent = _options.CategorySeoContent,
                        },
                        Products = BuildCategoryProducts(),
                    }, request),
                    var value when value.EndsWith("/public/catalog/products/slug/metro-runner", StringComparison.OrdinalIgnoreCase) => JsonResponse(new GetProduct
                    {
                        Id = MetroRunnerId,
                        Name = "Metro Runner",
                        Slug = "metro-runner",
                        Description = "Lightweight running shoe for everyday sessions.",
                        Image = "/uploads/metro-runner.png",
                        Price = 129.95m,
                        Quantity = 5,
                        CategoryId = SneakersCategoryId,
                        SeoContent = _options.ProductSeoContent,
                        CreatedOn = new DateTime(2026, 4, 18, 0, 0, 0, DateTimeKind.Utc),
                        Category = new GetCategory
                        {
                            Id = SneakersCategoryId,
                            Name = "Sneakers",
                            Slug = "sneakers",
                        },
                    }, request),
                    _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                    {
                        RequestMessage = request,
                    }),
                };
            }

            private IReadOnlyList<GetCatalogProduct> BuildListingProducts()
            {
                return BuildCategoryProducts();
            }

            private IReadOnlyList<GetCatalogProduct> BuildCategoryProducts()
            {
                var products = new List<GetCatalogProduct>
                {
                    new GetCatalogProduct
                    {
                        Id = MetroRunnerId,
                        Name = "Metro Runner",
                        Slug = "metro-runner",
                        Description = "Lightweight running shoe for everyday sessions.",
                        Image = "/uploads/metro-runner.png",
                        Price = 129.95m,
                        CategoryName = "Sneakers",
                        CategorySlug = "sneakers",
                        CategoryId = SneakersCategoryId,
                        CreatedOn = new DateTime(2026, 4, 18, 0, 0, 0, DateTimeKind.Utc),
                    },
                };

                if (_options.IncludeRelatedProducts)
                {
                    products.Add(new GetCatalogProduct
                    {
                        Id = TrailRunnerId,
                        Name = "Trail Runner",
                        Slug = "trail-runner",
                        Description = "Grippy trail shoe for mixed terrain runs.",
                        Image = "/uploads/trail-runner.png",
                        Price = 139.95m,
                        CategoryName = "Sneakers",
                        CategorySlug = "sneakers",
                        CategoryId = SneakersCategoryId,
                        CreatedOn = new DateTime(2026, 4, 17, 0, 0, 0, DateTimeKind.Utc),
                    });
                }

                return products;
            }

            private static Task<HttpResponseMessage> JsonResponse<T>(T payload, HttpRequestMessage request)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
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