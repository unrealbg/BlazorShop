namespace BlazorShop.Storefront.Services
{
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Storefront.Services.Contracts;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Product;

    public class StorefrontSeoComposer : IStorefrontSeoComposer
    {
        private readonly ISeoMetadataBuilder _metadataBuilder;
        private readonly IStorefrontSeoSettingsProvider _settingsProvider;

        public StorefrontSeoComposer(ISeoMetadataBuilder metadataBuilder, IStorefrontSeoSettingsProvider settingsProvider)
        {
            _metadataBuilder = metadataBuilder;
            _settingsProvider = settingsProvider;
        }

        public async Task<SeoMetadataDto> ComposeStaticPageAsync(string title, string relativePath, string fallbackMetaDescription, CancellationToken cancellationToken = default)
        {
            var settings = await _settingsProvider.GetAsync(cancellationToken);
            return _metadataBuilder.Build(new SeoMetadataBuildRequest
            {
                PageTitle = title,
                RelativePath = relativePath,
                Settings = settings,
                PageSeo = new SeoFieldsDto
                {
                    MetaDescription = fallbackMetaDescription,
                },
            });
        }

        public async Task<SeoMetadataDto> ComposeCategoryPageAsync(GetCategory category, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(category);

            var settings = await _settingsProvider.GetAsync(cancellationToken);
            return _metadataBuilder.Build(new SeoMetadataBuildRequest
            {
                PageTitle = $"{category.Name} Products",
                RelativePath = StorefrontRoutes.Category(category.Slug),
                Settings = settings,
                PageSeo = MapCategorySeo(category, $"Browse {category.Name} products, descriptions, pricing, and availability in the BlazorShop catalog."),
            });
        }

        public async Task<SeoMetadataDto> ComposeProductPageAsync(GetProduct product, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(product);

            var settings = await _settingsProvider.GetAsync(cancellationToken);
            return _metadataBuilder.Build(new SeoMetadataBuildRequest
            {
                PageTitle = product.Name,
                RelativePath = StorefrontRoutes.Product(product.Slug),
                Settings = settings,
                PageSeo = MapProductSeo(product, Truncate(product.Description, 160)),
            });
        }

        public async Task<SeoMetadataDto> ComposeServiceUnavailablePageAsync(string title, string relativePath, string fallbackMetaDescription, CancellationToken cancellationToken = default)
        {
            var settings = await _settingsProvider.GetAsync(cancellationToken);
            return _metadataBuilder.Build(new SeoMetadataBuildRequest
            {
                PageTitle = title,
                RelativePath = relativePath,
                Settings = settings,
                PageSeo = new SeoFieldsDto
                {
                    MetaDescription = fallbackMetaDescription,
                    RobotsIndex = false,
                    RobotsFollow = false,
                },
            });
        }

        public async Task<SeoMetadataDto> ComposeNotFoundPageAsync(string title, string relativePath, string fallbackMetaDescription, CancellationToken cancellationToken = default)
        {
            var settings = await _settingsProvider.GetAsync(cancellationToken);
            return _metadataBuilder.Build(new SeoMetadataBuildRequest
            {
                PageTitle = title,
                RelativePath = relativePath,
                Settings = settings,
                PageSeo = new SeoFieldsDto
                {
                    MetaDescription = fallbackMetaDescription,
                    RobotsIndex = false,
                    RobotsFollow = false,
                },
            });
        }

        private static SeoFieldsDto MapCategorySeo(GetCategory category, string fallbackMetaDescription)
        {
            return new SeoFieldsDto
            {
                MetaTitle = category.MetaTitle,
                MetaDescription = string.IsNullOrWhiteSpace(category.MetaDescription) ? fallbackMetaDescription : category.MetaDescription,
                CanonicalUrl = category.CanonicalUrl,
                OgTitle = category.OgTitle,
                OgDescription = category.OgDescription,
                OgImage = category.OgImage,
                RobotsIndex = category.RobotsIndex,
                RobotsFollow = category.RobotsFollow,
            };
        }

        private static SeoFieldsDto MapProductSeo(GetProduct product, string fallbackMetaDescription)
        {
            return new SeoFieldsDto
            {
                MetaTitle = product.MetaTitle,
                MetaDescription = string.IsNullOrWhiteSpace(product.MetaDescription) ? fallbackMetaDescription : product.MetaDescription,
                CanonicalUrl = product.CanonicalUrl,
                OgTitle = product.OgTitle,
                OgDescription = product.OgDescription,
                OgImage = product.OgImage,
                RobotsIndex = product.RobotsIndex,
                RobotsFollow = product.RobotsFollow,
            };
        }

        private static string Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            {
                return value ?? string.Empty;
            }

            return $"{value[..maxLength].TrimEnd()}...";
        }
    }
}