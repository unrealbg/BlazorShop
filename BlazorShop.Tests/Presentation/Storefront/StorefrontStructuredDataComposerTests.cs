namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Text.Json.Nodes;

    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Storefront.Services;
    using BlazorShop.Storefront.Services.Contracts;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Product;

    using Xunit;

    public class StorefrontStructuredDataComposerTests
    {
        private readonly IStorefrontStructuredDataComposer _composer;

        public StorefrontStructuredDataComposerTests()
        {
            _composer = new StorefrontStructuredDataComposer(
                new StubPublicUrlResolver("https://shop.example.com/"),
                new StubSeoSettingsProvider(new SeoSettingsDto
                {
                    SiteName = "BlazorShop",
                    CompanyName = "BlazorShop",
                    CompanyLogoUrl = "/assets/logo.png",
                    CompanyEmail = "support@shop.example.com",
                    CompanyPhone = "+1-555-0100",
                    BaseCanonicalUrl = "https://shop.example.com",
                    FacebookUrl = "https://facebook.com/blazorshop",
                }));
        }

        [Fact]
        public async Task ComposeHomePageAsync_ReturnsOrganizationAndWebsiteWithoutSearchAction()
        {
            var result = await _composer.ComposeHomePageAsync();

            var organization = GetNode(result, "Organization");
            var website = GetNode(result, "WebSite");

            Assert.Equal("BlazorShop", organization["name"]?.GetValue<string>());
            Assert.Equal("https://shop.example.com/", organization["url"]?.GetValue<string>());
            Assert.Equal("https://shop.example.com/assets/logo.png", organization["logo"]?.GetValue<string>());
            Assert.Equal("BlazorShop", website["name"]?.GetValue<string>());
            Assert.Equal("https://shop.example.com/", website["url"]?.GetValue<string>());
            Assert.False(website.ContainsKey("potentialAction"));
        }

        [Fact]
        public async Task ComposeCategoryPageAsync_ReturnsCollectionPageAndBreadcrumbsWithSlugRoutes()
        {
            var result = await _composer.ComposeCategoryPageAsync(new GetCategory
            {
                Name = "Sneakers",
                Slug = "sneakers",
                MetaDescription = "Browse the latest sneakers.",
            });

            var breadcrumb = GetNode(result, "BreadcrumbList");
            var collectionPage = GetNode(result, "CollectionPage");
            var items = Assert.IsType<JsonArray>(breadcrumb["itemListElement"]);

            Assert.Equal(2, items.Count);
            Assert.Equal("https://shop.example.com/", items[0]?["item"]?.GetValue<string>());
            Assert.Equal("https://shop.example.com/category/sneakers", items[1]?["item"]?.GetValue<string>());
            Assert.Equal("Sneakers", collectionPage["name"]?.GetValue<string>());
            Assert.Equal("https://shop.example.com/category/sneakers", collectionPage["url"]?.GetValue<string>());
        }

        [Fact]
        public async Task ComposeProductPageAsync_ReturnsProductOfferBreadcrumbAndOmitsUnsupportedFields()
        {
            var result = await _composer.ComposeProductPageAsync(new GetProduct
            {
                Name = "Metro Runner",
                Slug = "metro-runner",
                Description = "Lightweight running shoe for everyday sessions.",
                Image = "/uploads/metro-runner.png",
                Price = 129.95m,
                Category = new GetCategory
                {
                    Name = "Sneakers",
                    Slug = "sneakers",
                },
            });

            var breadcrumb = GetNode(result, "BreadcrumbList");
            var product = GetNode(result, "Product");
            var offer = Assert.IsType<JsonObject>(product["offers"]);
            var items = Assert.IsType<JsonArray>(breadcrumb["itemListElement"]);

            Assert.Equal(3, items.Count);
            Assert.Equal("https://shop.example.com/category/sneakers", items[1]?["item"]?.GetValue<string>());
            Assert.Equal("https://shop.example.com/product/metro-runner", items[2]?["item"]?.GetValue<string>());
            Assert.Equal("https://shop.example.com/uploads/metro-runner.png", product["image"]?.GetValue<string>());
            Assert.Equal("Sneakers", product["category"]?.GetValue<string>());
            Assert.Equal("https://shop.example.com/product/metro-runner", offer["url"]?.GetValue<string>());
            Assert.False(product.ContainsKey("aggregateRating"));
            Assert.False(product.ContainsKey("review"));
            Assert.False(product.ContainsKey("brand"));
            Assert.False(offer.ContainsKey("availability"));
            Assert.False(offer.ContainsKey("priceCurrency"));
        }

        [Fact]
        public async Task ComposeFaqPageAsync_MapsRealQuestionAnswerPairs()
        {
            var result = await _composer.ComposeFaqPageAsync(
                "Frequently Asked Questions",
                StorefrontRoutes.Faq,
                "Common answers for storefront visitors.",
                [
                    new StorefrontFaqStructuredDataItem("Do all products appear publicly?", "No. Only published products with public SEO slugs are exposed."),
                    new StorefrontFaqStructuredDataItem("Is redirect automation included?", "No. Redirect automation is intentionally deferred."),
                ]);

            var faqPage = GetNode(result, "FAQPage");
            var mainEntity = Assert.IsType<JsonArray>(faqPage["mainEntity"]);

            Assert.Equal(2, mainEntity.Count);
            Assert.Equal("Question", mainEntity[0]?["@type"]?.GetValue<string>());
            Assert.Equal("Answer", mainEntity[0]?["acceptedAnswer"]?["@type"]?.GetValue<string>());
        }

        private static JsonObject GetNode(StorefrontStructuredDataDocument document, string schemaType)
        {
            var graph = Assert.IsType<JsonArray>(document.Payload["@graph"]);

            return Assert.IsType<JsonObject>(graph.Single(node => string.Equals(node?["@type"]?.GetValue<string>(), schemaType, StringComparison.Ordinal)));
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

        private sealed class StubPublicUrlResolver : IStorefrontPublicUrlResolver
        {
            private readonly string _baseUrl;

            public StubPublicUrlResolver(string baseUrl)
            {
                _baseUrl = baseUrl;
            }

            public string? ResolveBaseUrl(string? configuredBaseUrl = null)
            {
                return NormalizeAbsoluteUrl(string.IsNullOrWhiteSpace(configuredBaseUrl) ? _baseUrl : configuredBaseUrl);
            }

            public string? ResolveAbsoluteUrl(string? relativeOrAbsoluteUrl, string? configuredBaseUrl = null)
            {
                if (string.IsNullOrWhiteSpace(relativeOrAbsoluteUrl))
                {
                    return null;
                }

                if (TryCreateHttpAbsoluteUri(relativeOrAbsoluteUrl, out var absoluteUri))
                {
                    return absoluteUri.ToString();
                }

                return new Uri(new Uri(ResolveBaseUrl(configuredBaseUrl)!, UriKind.Absolute), relativeOrAbsoluteUrl).ToString();
            }

            private static string? NormalizeAbsoluteUrl(string? candidate)
            {
                if (string.IsNullOrWhiteSpace(candidate)
                    || !Uri.TryCreate(candidate, UriKind.Absolute, out var absoluteUri))
                {
                    return null;
                }

                var builder = new UriBuilder(absoluteUri)
                {
                    Fragment = string.Empty,
                    Query = string.Empty,
                    Path = string.IsNullOrWhiteSpace(absoluteUri.AbsolutePath) || absoluteUri.AbsolutePath == "/"
                        ? "/"
                        : absoluteUri.AbsolutePath.EndsWith("/", StringComparison.Ordinal)
                            ? absoluteUri.AbsolutePath
                            : $"{absoluteUri.AbsolutePath}/",
                };

                return builder.Uri.ToString();
            }

            private static bool TryCreateHttpAbsoluteUri(string value, out Uri uri)
            {
                return Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri!)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            }
        }
    }
}
