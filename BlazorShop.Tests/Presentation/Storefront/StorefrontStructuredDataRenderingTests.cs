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

    public class StorefrontStructuredDataRenderingTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public StorefrontStructuredDataRenderingTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task HomePage_RendersOrganizationAndWebsiteJsonLd()
        {
            using var client = CreateClient();

            using var response = await client.GetAsync("/");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"@type\":\"Organization\"", content, StringComparison.Ordinal);
            Assert.Contains("\"@type\":\"WebSite\"", content, StringComparison.Ordinal);
            Assert.Contains("\"url\":\"https://shop.example.com/\"", content, StringComparison.Ordinal);
        }

        [Fact]
        public async Task CategoryPage_RendersBreadcrumbAndCollectionPageJsonLd()
        {
            using var client = CreateClient();

            using var response = await client.GetAsync("/category/sneakers");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"@type\":\"BreadcrumbList\"", content, StringComparison.Ordinal);
            Assert.Contains("\"@type\":\"CollectionPage\"", content, StringComparison.Ordinal);
            Assert.Contains("https://shop.example.com/category/sneakers", content, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ProductPage_RendersProductOfferJsonLdWithoutUnsupportedRatingFields()
        {
            using var client = CreateClient();

            using var response = await client.GetAsync("/product/metro-runner");
            var content = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"@type\":\"Product\"", content, StringComparison.Ordinal);
            Assert.Contains("\"@type\":\"Offer\"", content, StringComparison.Ordinal);
            Assert.Contains("https://shop.example.com/product/metro-runner", content, StringComparison.Ordinal);
            Assert.DoesNotContain("aggregateRating", content, StringComparison.Ordinal);
            Assert.DoesNotContain("reviewCount", content, StringComparison.Ordinal);
        }

        private HttpClient CreateClient()
        {
            var factory = _factory.WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<StorefrontApiClient>();
                    services.RemoveAll<IStorefrontSeoSettingsProvider>();

                    services.AddScoped(_ => new StorefrontApiClient(new HttpClient(new StructuredDataHttpMessageHandler())
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

        private sealed class StructuredDataHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var path = request.RequestUri?.AbsolutePath ?? string.Empty;

                return path switch
                {
                    var value when value.EndsWith("/public/catalog/categories", StringComparison.OrdinalIgnoreCase) => JsonResponse<IReadOnlyList<GetCategory>>(
                    [
                        new GetCategory { Id = Guid.NewGuid(), Name = "Sneakers", Slug = "sneakers" },
                    ],
                    request),
                    var value when value.EndsWith("/public/catalog/products", StringComparison.OrdinalIgnoreCase) => JsonResponse(new PagedResult<GetCatalogProduct>
                    {
                        Items =
                        [
                            new GetCatalogProduct
                            {
                                Id = Guid.NewGuid(),
                                Name = "Metro Runner",
                                Slug = "metro-runner",
                                Description = "Lightweight running shoe for everyday sessions.",
                                Image = "/uploads/metro-runner.png",
                                Price = 129.95m,
                                CategoryName = "Sneakers",
                                CategorySlug = "sneakers",
                                CategoryId = Guid.NewGuid(),
                                CreatedOn = new DateTime(2026, 4, 18, 0, 0, 0, DateTimeKind.Utc),
                            },
                        ],
                        PageNumber = 1,
                        PageSize = 12,
                        TotalCount = 1,
                    }, request),
                    var value when value.EndsWith("/public/catalog/categories/slug/sneakers", StringComparison.OrdinalIgnoreCase) => JsonResponse(new GetCategoryPage
                    {
                        Category = new GetCategory
                        {
                            Id = Guid.NewGuid(),
                            Name = "Sneakers",
                            Slug = "sneakers",
                            MetaDescription = "Browse the latest sneakers.",
                        },
                        Products =
                        [
                            new GetCatalogProduct
                            {
                                Id = Guid.NewGuid(),
                                Name = "Metro Runner",
                                Slug = "metro-runner",
                                Description = "Lightweight running shoe for everyday sessions.",
                                Image = "/uploads/metro-runner.png",
                                Price = 129.95m,
                                CategoryName = "Sneakers",
                                CategorySlug = "sneakers",
                                CategoryId = Guid.NewGuid(),
                                CreatedOn = new DateTime(2026, 4, 18, 0, 0, 0, DateTimeKind.Utc),
                            },
                        ],
                    }, request),
                    var value when value.EndsWith("/public/catalog/products/slug/metro-runner", StringComparison.OrdinalIgnoreCase) => JsonResponse(new GetProduct
                    {
                        Id = Guid.NewGuid(),
                        Name = "Metro Runner",
                        Slug = "metro-runner",
                        Description = "Lightweight running shoe for everyday sessions.",
                        Image = "/uploads/metro-runner.png",
                        Price = 129.95m,
                        Quantity = 5,
                        CategoryId = Guid.NewGuid(),
                        Category = new GetCategory
                        {
                            Id = Guid.NewGuid(),
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