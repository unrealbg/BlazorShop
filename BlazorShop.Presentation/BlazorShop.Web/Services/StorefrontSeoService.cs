namespace BlazorShop.Web.Services
{
    using BlazorShop.Web.Services.Contracts;
    using BlazorShop.Web.Shared.Models.Seo;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class StorefrontSeoService : IStorefrontSeoService
    {
        private readonly IStorefrontSeoMetadataBuilder _metadataBuilder;
        private readonly ISeoSettingsService _seoSettingsService;

        private bool _hasLoadedSettings;
        private GetSeoSettings? _cachedSettings;

        public StorefrontSeoService(IStorefrontSeoMetadataBuilder metadataBuilder, ISeoSettingsService seoSettingsService)
        {
            _metadataBuilder = metadataBuilder;
            _seoSettingsService = seoSettingsService;
        }

        public async Task<StorefrontSeoMetadata> BuildAsync(StorefrontSeoMetadataBuildRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var effectiveRequest = new StorefrontSeoMetadataBuildRequest
            {
                PageTitle = request.PageTitle,
                RelativePath = request.RelativePath,
                FallbackMetaDescription = request.FallbackMetaDescription,
                FallbackOgImage = request.FallbackOgImage,
                PageSeo = request.PageSeo,
                Settings = request.Settings ?? await GetSettingsAsync(),
            };

            return _metadataBuilder.Build(effectiveRequest);
        }

        private async Task<GetSeoSettings?> GetSettingsAsync()
        {
            if (_hasLoadedSettings)
            {
                return _cachedSettings;
            }

            try
            {
                var result = await _seoSettingsService.GetAsync();
                if (!result.Success)
                {
                    return null;
                }

                _cachedSettings = result.Data;
                _hasLoadedSettings = true;

                return _cachedSettings;
            }
            catch
            {
                return null;
            }
        }
    }
}