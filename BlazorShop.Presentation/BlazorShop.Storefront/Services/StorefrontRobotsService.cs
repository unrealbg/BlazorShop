namespace BlazorShop.Storefront.Services
{
    using System.Text;

    using BlazorShop.Storefront.Services.Contracts;

    public class StorefrontRobotsService : IStorefrontRobotsService
    {
        private static readonly string[] DisallowPaths =
        [
            "/api/",
            "/swagger/",
            "/admin/",
            "/account/",
            "/authentication/",
            "/_framework/",
            "/_content/",
            "/_blazor/",
        ];

        private readonly IStorefrontPublicUrlResolver _publicUrlResolver;
        private readonly IStorefrontSeoSettingsProvider _seoSettingsProvider;

        public StorefrontRobotsService(IStorefrontPublicUrlResolver publicUrlResolver, IStorefrontSeoSettingsProvider seoSettingsProvider)
        {
            _publicUrlResolver = publicUrlResolver;
            _seoSettingsProvider = seoSettingsProvider;
        }

        public async Task<string> GenerateAsync(CancellationToken cancellationToken = default)
        {
            var settings = await _seoSettingsProvider.GetAsync(cancellationToken);
            var sitemapUrl = _publicUrlResolver.ResolveAbsoluteUrl(StorefrontRoutes.Sitemap, settings?.BaseCanonicalUrl)
                ?? StorefrontRoutes.Sitemap;

            var builder = new StringBuilder();
            builder.AppendLine("User-agent: *");
            builder.AppendLine("Allow: /");

            foreach (var path in DisallowPaths)
            {
                builder.AppendLine($"Disallow: {path}");
            }

            builder.AppendLine($"Sitemap: {sitemapUrl}");

            return builder.ToString();
        }
    }
}