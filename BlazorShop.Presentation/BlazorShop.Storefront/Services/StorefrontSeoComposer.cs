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
        private readonly IStorefrontPublicUrlResolver _publicUrlResolver;
        private readonly IStorefrontSeoSettingsProvider _settingsProvider;

        public StorefrontSeoComposer(
            ISeoMetadataBuilder metadataBuilder,
            IStorefrontPublicUrlResolver publicUrlResolver,
            IStorefrontSeoSettingsProvider settingsProvider)
        {
            _metadataBuilder = metadataBuilder;
            _publicUrlResolver = publicUrlResolver;
            _settingsProvider = settingsProvider;
        }

        public async Task<SeoMetadataDto> ComposeStaticPageAsync(string title, string relativePath, string fallbackMetaDescription, CancellationToken cancellationToken = default)
        {
            var settings = await GetEffectiveSettingsAsync(cancellationToken);
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

            var settings = await GetEffectiveSettingsAsync(cancellationToken);
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

            var settings = await GetEffectiveSettingsAsync(cancellationToken);
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
            var settings = await GetEffectiveSettingsAsync(cancellationToken);
            return _metadataBuilder.Build(new SeoMetadataBuildRequest
            {
                PageTitle = title,
                RelativePath = relativePath,
                SuppressCanonicalUrl = true,
                SuppressOpenGraph = true,
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
            var settings = await GetEffectiveSettingsAsync(cancellationToken);
            return _metadataBuilder.Build(new SeoMetadataBuildRequest
            {
                PageTitle = title,
                RelativePath = relativePath,
                SuppressCanonicalUrl = true,
                SuppressOpenGraph = true,
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
                OgTitle = product.OgTitle,
                OgDescription = product.OgDescription,
                OgImage = product.OgImage,
                RobotsIndex = product.RobotsIndex,
                RobotsFollow = product.RobotsFollow,
            };
        }

        private async Task<SeoSettingsDto?> GetEffectiveSettingsAsync(CancellationToken cancellationToken)
        {
            var settings = await _settingsProvider.GetAsync(cancellationToken);
            var resolvedBaseUrl = _publicUrlResolver.ResolveBaseUrl(settings?.BaseCanonicalUrl);

            if (string.IsNullOrWhiteSpace(resolvedBaseUrl))
            {
                return settings;
            }

            if (settings is null)
            {
                return new SeoSettingsDto
                {
                    BaseCanonicalUrl = resolvedBaseUrl,
                };
            }

            if (string.Equals(settings.BaseCanonicalUrl, resolvedBaseUrl, StringComparison.OrdinalIgnoreCase))
            {
                return settings;
            }

            return CloneSettings(settings, resolvedBaseUrl);
        }

        private static SeoSettingsDto CloneSettings(SeoSettingsDto settings, string baseCanonicalUrl)
        {
            return new SeoSettingsDto
            {
                Id = settings.Id,
                SiteName = settings.SiteName,
                DefaultTitleSuffix = settings.DefaultTitleSuffix,
                DefaultMetaDescription = settings.DefaultMetaDescription,
                DefaultOgImage = settings.DefaultOgImage,
                BaseCanonicalUrl = baseCanonicalUrl,
                CompanyName = settings.CompanyName,
                CompanyLogoUrl = settings.CompanyLogoUrl,
                CompanyPhone = settings.CompanyPhone,
                CompanyEmail = settings.CompanyEmail,
                CompanyAddress = settings.CompanyAddress,
                FacebookUrl = settings.FacebookUrl,
                InstagramUrl = settings.InstagramUrl,
                XUrl = settings.XUrl,
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