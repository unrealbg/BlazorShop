namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;
    using System.Net.Http.Json;

    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Storefront.Services;
    using BlazorShop.Storefront.Services.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Discovery;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Models.Seo;

    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.DependencyInjection.Extensions;

    public sealed class StorefrontSeoAuditScenario
    {
        private static readonly Guid SneakersCategoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid BootsCategoryId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        private static readonly Guid MetroRunnerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private static readonly Guid TrailRunnerId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        private static readonly Guid SummitBootId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        private static readonly DateTime SneakersLastModifiedUtc = new(2026, 4, 14, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime MetroRunnerLastModifiedUtc = new(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime TrailRunnerLastModifiedUtc = new(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc);

        public const string BaseUrl = "https://shop.example.com";

        public HttpStatusCode CategoriesStatusCode { get; init; } = HttpStatusCode.OK;

        public HttpStatusCode CatalogProductsStatusCode { get; init; } = HttpStatusCode.OK;

        public HttpStatusCode CategoryPageStatusCode { get; init; } = HttpStatusCode.OK;

        public HttpStatusCode ProductPageStatusCode { get; init; } = HttpStatusCode.OK;

        public HttpStatusCode SitemapStatusCode { get; init; } = HttpStatusCode.OK;

        public SeoSettingsDto SeoSettings { get; init; } = CreateSeoSettings();

        public IReadOnlyList<GetCategory> Categories { get; init; } = CreateCategories();

        public PagedResult<GetCatalogProduct> CatalogPage { get; init; } = CreateCatalogPage();

        public GetCategoryPage CategoryPage { get; init; } = CreateCategoryPage();

        public GetProduct Product { get; init; } = CreateProduct();

        public GetPublicCatalogSitemap Sitemap { get; init; } = CreateSitemap();

        public IReadOnlyDictionary<string, SeoRedirectResolutionDto> Redirects { get; init; } = CreateRedirects();

        public static string AbsoluteUrl(string relativePath)
        {
            return relativePath == StorefrontRoutes.Home
                ? $"{BaseUrl}/"
                : new Uri(new Uri($"{BaseUrl}/", UriKind.Absolute), relativePath).ToString();
        }

        private static SeoSettingsDto CreateSeoSettings()
        {
            return new SeoSettingsDto
            {
                SiteName = "BlazorShop",
                CompanyName = "BlazorShop",
                DefaultTitleSuffix = "| BlazorShop",
                DefaultMetaDescription = "Shop the published BlazorShop catalog.",
                DefaultOgImage = "/images/og/default-storefront.png",
                BaseCanonicalUrl = BaseUrl,
                CompanyLogoUrl = "/assets/logo.png",
                CompanyEmail = "support@shop.example.com",
                CompanyPhone = "+1-555-0100",
            };
        }

        private static IReadOnlyList<GetCategory> CreateCategories()
        {
            return
            [
                new GetCategory
                {
                    Id = SneakersCategoryId,
                    Name = "Sneakers",
                    Slug = "sneakers",
                    MetaDescription = "Browse the latest sneakers.",
                    SeoContent = "Designed for daily mileage and weekend recovery walks.",
                },
                new GetCategory
                {
                    Id = BootsCategoryId,
                    Name = "Boots",
                    Slug = "boots",
                    MetaDescription = "Discover weather-ready boots.",
                },
            ];
        }

        private static PagedResult<GetCatalogProduct> CreateCatalogPage()
        {
            var items = new List<GetCatalogProduct>
            {
                CreateCatalogProduct(MetroRunnerId, "Metro Runner", "metro-runner", SneakersCategoryId, "Sneakers", "sneakers", MetroRunnerLastModifiedUtc, 129.95m, "/uploads/metro-runner.png", "Lightweight running shoe for everyday sessions."),
                CreateCatalogProduct(TrailRunnerId, "Trail Runner", "trail-runner", SneakersCategoryId, "Sneakers", "sneakers", TrailRunnerLastModifiedUtc, 139.95m, "/uploads/trail-runner.png", "Grippy trail shoe for mixed terrain runs."),
                CreateCatalogProduct(SummitBootId, "Summit Boot", "summit-boot", BootsCategoryId, "Boots", "boots", new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc), 179.95m, "/uploads/summit-boot.png", "All-weather boot for colder days."),
            };

            return new PagedResult<GetCatalogProduct>
            {
                Items = items,
                PageNumber = 1,
                PageSize = 24,
                TotalCount = items.Count,
            };
        }

        private static GetCategoryPage CreateCategoryPage()
        {
            return new GetCategoryPage
            {
                Category = new GetCategory
                {
                    Id = SneakersCategoryId,
                    Name = "Sneakers",
                    Slug = "sneakers",
                    MetaDescription = "Browse the latest sneakers.",
                    SeoContent = "Designed for daily mileage and weekend recovery walks.",
                },
                Products =
                [
                    CreateCatalogProduct(MetroRunnerId, "Metro Runner", "metro-runner", SneakersCategoryId, "Sneakers", "sneakers", MetroRunnerLastModifiedUtc, 129.95m, "/uploads/metro-runner.png", "Lightweight running shoe for everyday sessions."),
                    CreateCatalogProduct(TrailRunnerId, "Trail Runner", "trail-runner", SneakersCategoryId, "Sneakers", "sneakers", TrailRunnerLastModifiedUtc, 139.95m, "/uploads/trail-runner.png", "Grippy trail shoe for mixed terrain runs."),
                ],
            };
        }

        private static GetProduct CreateProduct()
        {
            return new GetProduct
            {
                Id = MetroRunnerId,
                Name = "Metro Runner",
                Slug = "metro-runner",
                Description = "Lightweight running shoe for everyday sessions.",
                Image = "/uploads/metro-runner.png",
                Price = 129.95m,
                Quantity = 5,
                CategoryId = SneakersCategoryId,
                SeoContent = "Metro Runner adds breathable mesh and responsive cushioning.",
                CreatedOn = MetroRunnerLastModifiedUtc,
                Category = new GetCategory
                {
                    Id = SneakersCategoryId,
                    Name = "Sneakers",
                    Slug = "sneakers",
                },
            };
        }

        private static GetPublicCatalogSitemap CreateSitemap()
        {
            return new GetPublicCatalogSitemap
            {
                Categories =
                [
                    new GetCategorySitemapEntry { Slug = "sneakers", LastModifiedUtc = SneakersLastModifiedUtc },
                    new GetCategorySitemapEntry { Slug = "boots", LastModifiedUtc = new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc) },
                ],
                Products =
                [
                    new GetProductSitemapEntry { Slug = "metro-runner", LastModifiedUtc = MetroRunnerLastModifiedUtc },
                    new GetProductSitemapEntry { Slug = "trail-runner", LastModifiedUtc = TrailRunnerLastModifiedUtc },
                    new GetProductSitemapEntry { Slug = "summit-boot", LastModifiedUtc = new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc) },
                ],
            };
        }

        private static IReadOnlyDictionary<string, SeoRedirectResolutionDto> CreateRedirects()
        {
            return new Dictionary<string, SeoRedirectResolutionDto>(StringComparer.OrdinalIgnoreCase)
            {
                ["/product/legacy-runner"] = new SeoRedirectResolutionDto
                {
                    NewPath = "/product/metro-runner",
                    StatusCode = 301,
                },
                ["/category/legacy-sneakers"] = new SeoRedirectResolutionDto
                {
                    NewPath = "/category/sneakers",
                    StatusCode = 301,
                },
                ["/legacy-sale"] = new SeoRedirectResolutionDto
                {
                    NewPath = "/todays-deals",
                    StatusCode = 301,
                },
            };
        }

        private static GetCatalogProduct CreateCatalogProduct(
            Guid id,
            string name,
            string slug,
            Guid categoryId,
            string categoryName,
            string categorySlug,
            DateTime createdOn,
            decimal price,
            string image,
            string description)
        {
            return new GetCatalogProduct
            {
                Id = id,
                Name = name,
                Slug = slug,
                CategoryId = categoryId,
                CategoryName = categoryName,
                CategorySlug = categorySlug,
                CreatedOn = createdOn,
                Price = price,
                Image = image,
                Description = description,
            };
        }
    }

    internal static class StorefrontSeoAuditClientFactory
    {
        public static HttpClient CreateClient(WebApplicationFactory<Program> factory, StorefrontSeoAuditScenario? scenario = null)
        {
            scenario ??= new StorefrontSeoAuditScenario();

            var configuredFactory = factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<StorefrontApiClient>();
                    services.RemoveAll<IStorefrontSeoSettingsProvider>();

                    services.AddScoped(_ => new StorefrontApiClient(new HttpClient(new StorefrontSeoAuditHttpMessageHandler(scenario))
                    {
                        BaseAddress = new Uri("https://api.example.com/api/"),
                    }));
                    services.AddScoped<IStorefrontSeoSettingsProvider>(_ => new StubSeoSettingsProvider(scenario.SeoSettings));
                });
            });

            return configuredFactory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });
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

        private sealed class StorefrontSeoAuditHttpMessageHandler : HttpMessageHandler
        {
            private readonly StorefrontSeoAuditScenario _scenario;

            public StorefrontSeoAuditHttpMessageHandler(StorefrontSeoAuditScenario scenario)
            {
                _scenario = scenario;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var path = request.RequestUri?.AbsolutePath ?? string.Empty;

                if (path.EndsWith("/public/seo/redirects/resolve", StringComparison.OrdinalIgnoreCase))
                {
                    return ResolveRedirectAsync(request);
                }

                if (path.EndsWith("/public/catalog/sitemap", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateResponseAsync(_scenario.SitemapStatusCode, _scenario.Sitemap, request);
                }

                if (TryGetSlug(path, "/public/catalog/categories/slug/", out var categorySlug))
                {
                    return string.Equals(categorySlug, "sneakers", StringComparison.OrdinalIgnoreCase)
                        ? CreateResponseAsync(_scenario.CategoryPageStatusCode, _scenario.CategoryPage, request)
                        : CreateStatusResponseAsync(HttpStatusCode.NotFound, request);
                }

                if (TryGetSlug(path, "/public/catalog/products/slug/", out var productSlug))
                {
                    return string.Equals(productSlug, "metro-runner", StringComparison.OrdinalIgnoreCase)
                        ? CreateResponseAsync(_scenario.ProductPageStatusCode, _scenario.Product, request)
                        : CreateStatusResponseAsync(HttpStatusCode.NotFound, request);
                }

                if (path.EndsWith("/public/catalog/categories", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateResponseAsync(_scenario.CategoriesStatusCode, _scenario.Categories, request);
                }

                if (path.EndsWith("/public/catalog/products", StringComparison.OrdinalIgnoreCase))
                {
                    return CreateResponseAsync(_scenario.CatalogProductsStatusCode, _scenario.CatalogPage, request);
                }

                return CreateStatusResponseAsync(HttpStatusCode.NotFound, request);
            }

            private Task<HttpResponseMessage> ResolveRedirectAsync(HttpRequestMessage request)
            {
                var query = request.RequestUri?.Query ?? string.Empty;
                var path = Uri.UnescapeDataString(query);
                if (path.StartsWith("?path=", StringComparison.OrdinalIgnoreCase))
                {
                    path = path[6..];
                }

                return _scenario.Redirects.TryGetValue(path, out var redirect)
                    ? CreateResponseAsync(HttpStatusCode.OK, redirect, request)
                    : CreateStatusResponseAsync(HttpStatusCode.NotFound, request);
            }

            private static bool TryGetSlug(string path, string prefix, out string slug)
            {
                var index = path.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    slug = string.Empty;
                    return false;
                }

                slug = path[(index + prefix.Length)..];
                return !string.IsNullOrWhiteSpace(slug);
            }

            private static Task<HttpResponseMessage> CreateResponseAsync<T>(HttpStatusCode statusCode, T payload, HttpRequestMessage request)
            {
                if (statusCode != HttpStatusCode.OK)
                {
                    return CreateStatusResponseAsync(statusCode, request);
                }

                return Task.FromResult(new HttpResponseMessage(statusCode)
                {
                    Content = JsonContent.Create(payload),
                    RequestMessage = request,
                });
            }

            private static Task<HttpResponseMessage> CreateStatusResponseAsync(HttpStatusCode statusCode, HttpRequestMessage request)
            {
                return Task.FromResult(new HttpResponseMessage(statusCode)
                {
                    RequestMessage = request,
                });
            }
        }
    }
}