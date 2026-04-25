namespace BlazorShop.Storefront.Services
{
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Storefront.Services.Contracts;

    using Microsoft.Extensions.Caching.Memory;

    public class StorefrontSeoSettingsProvider : IStorefrontSeoSettingsProvider
    {
        private const string CacheKey = "storefront-seo-settings";

        private readonly StorefrontApiClient _apiClient;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _memoryCache;

        public StorefrontSeoSettingsProvider(StorefrontApiClient apiClient, IConfiguration configuration, IMemoryCache memoryCache)
        {
            _apiClient = apiClient;
            _configuration = configuration;
            _memoryCache = memoryCache;
        }

        public async Task<SeoSettingsDto?> GetAsync(CancellationToken cancellationToken = default)
        {
            if (_memoryCache.TryGetValue(CacheKey, out SeoSettingsDto? cachedSettings))
            {
                return cachedSettings;
            }

            var settingsResult = await _apiClient.GetSeoSettingsAsync(cancellationToken);
            if (!settingsResult.IsSuccess || settingsResult.Value is null)
            {
                var fallbackSettings = CreateFallbackSettings();
                _memoryCache.Set(CacheKey, fallbackSettings, TimeSpan.FromMinutes(5));
                return fallbackSettings;
            }

            var settings = settingsResult.Value;

            var mappedSettings = new SeoSettingsDto
            {
                Id = settings.Id,
                SiteName = settings.SiteName,
                DefaultTitleSuffix = settings.DefaultTitleSuffix,
                DefaultMetaDescription = settings.DefaultMetaDescription,
                DefaultOgImage = settings.DefaultOgImage,
                BaseCanonicalUrl = settings.BaseCanonicalUrl,
                CompanyName = settings.CompanyName,
                CompanyLogoUrl = settings.CompanyLogoUrl,
                CompanyPhone = settings.CompanyPhone,
                CompanyEmail = settings.CompanyEmail,
                CompanyAddress = settings.CompanyAddress,
                FacebookUrl = settings.FacebookUrl,
                InstagramUrl = settings.InstagramUrl,
                XUrl = settings.XUrl,
            };

            _memoryCache.Set(CacheKey, mappedSettings, TimeSpan.FromMinutes(5));
            return mappedSettings;
        }

        private SeoSettingsDto CreateFallbackSettings()
        {
            return new SeoSettingsDto
            {
                SiteName = _configuration["StorefrontSeo:SiteName"] ?? "BlazorShop",
                DefaultTitleSuffix = _configuration["StorefrontSeo:DefaultTitleSuffix"] ?? "| BlazorShop",
                DefaultMetaDescription = _configuration["StorefrontSeo:DefaultMetaDescription"] ?? "Discover published categories and products across the BlazorShop storefront.",
                DefaultOgImage = _configuration["StorefrontSeo:DefaultOgImage"],
                BaseCanonicalUrl = _configuration["StorefrontSeo:BaseCanonicalUrl"],
                CompanyName = _configuration["StorefrontSeo:CompanyName"],
                CompanyLogoUrl = _configuration["StorefrontSeo:CompanyLogoUrl"],
                CompanyPhone = _configuration["StorefrontSeo:CompanyPhone"],
                CompanyEmail = _configuration["StorefrontSeo:CompanyEmail"],
                CompanyAddress = _configuration["StorefrontSeo:CompanyAddress"],
                FacebookUrl = _configuration["StorefrontSeo:FacebookUrl"],
                InstagramUrl = _configuration["StorefrontSeo:InstagramUrl"],
                XUrl = _configuration["StorefrontSeo:XUrl"],
            };
        }
    }
}