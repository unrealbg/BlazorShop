namespace BlazorShop.Tests.Presentation.Storefront
{
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services;
    using BlazorShop.Storefront.Services;
    using BlazorShop.Storefront.Services.Contracts;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Product;

    using Xunit;

    public class StorefrontSeoComposerTests
    {
        private readonly IStorefrontSeoComposer _composer;

        public StorefrontSeoComposerTests()
        {
            _composer = new StorefrontSeoComposer(
                new SeoMetadataBuilder(),
                new StubPublicUrlResolver("https://shop.example.com/"),
                new StubSeoSettingsProvider(new SeoSettingsDto
                {
                    SiteName = "BlazorShop",
                    DefaultTitleSuffix = "| BlazorShop",
                    DefaultMetaDescription = "Shop the published BlazorShop catalog.",
                    BaseCanonicalUrl = "https://shop.example.com",
                }));
        }

        [Fact]
        public async Task ComposeCategoryPageAsync_UsesCanonicalSlugRoute()
        {
            var category = new GetCategory
            {
                Name = "Shoes",
                Slug = "shoes",
                MetaTitle = "Shop Shoes",
                CanonicalUrl = "https://legacy.example.com/shoes",
                RobotsIndex = false,
                RobotsFollow = true,
            };

            var result = await _composer.ComposeCategoryPageAsync(category);

            Assert.Equal("Shop Shoes | BlazorShop", result.Title);
            Assert.Equal("https://shop.example.com/category/shoes", result.CanonicalUrl);
            Assert.False(result.RobotsIndex);
            Assert.True(result.RobotsFollow);
        }

        [Fact]
        public async Task ComposeProductPageAsync_UsesCanonicalSlugRouteAndDescriptionFallback()
        {
            var product = new GetProduct
            {
                Name = "Running Shoes",
                Slug = "running-shoes",
                Description = "Lightweight shoes for everyday training.",
                CanonicalUrl = "https://legacy.example.com/running-shoes",
                RobotsIndex = true,
                RobotsFollow = false,
            };

            var result = await _composer.ComposeProductPageAsync(product);

            Assert.Equal("Running Shoes | BlazorShop", result.Title);
            Assert.Equal("Lightweight shoes for everyday training.", result.MetaDescription);
            Assert.Equal("https://shop.example.com/product/running-shoes", result.CanonicalUrl);
            Assert.True(result.RobotsIndex);
            Assert.False(result.RobotsFollow);
        }

        [Fact]
        public async Task ComposeNotFoundPageAsync_MarksPageAsNoIndex()
        {
            var result = await _composer.ComposeNotFoundPageAsync(
                "Product not found",
                "/product/missing-product",
                "We couldn't find a published product for this address.");

            Assert.Null(result.CanonicalUrl);
            Assert.Null(result.OgTitle);
            Assert.Null(result.OgDescription);
            Assert.Null(result.OgImage);
            Assert.Null(result.SiteName);
            Assert.False(result.RobotsIndex);
            Assert.False(result.RobotsFollow);
        }

        [Fact]
        public async Task ComposeServiceUnavailablePageAsync_MarksPageAsNoIndex()
        {
            var result = await _composer.ComposeServiceUnavailablePageAsync(
                "Catalog temporarily unavailable",
                "/",
                "The storefront is running, but the catalog API is not reachable right now.");

            Assert.Null(result.CanonicalUrl);
            Assert.Null(result.OgTitle);
            Assert.Null(result.OgDescription);
            Assert.Null(result.OgImage);
            Assert.Null(result.SiteName);
            Assert.False(result.RobotsIndex);
            Assert.False(result.RobotsFollow);
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
                return string.IsNullOrWhiteSpace(configuredBaseUrl) ? _baseUrl : configuredBaseUrl;
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

            private static bool TryCreateHttpAbsoluteUri(string value, out Uri uri)
            {
                return Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri!)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            }
        }
    }
}
