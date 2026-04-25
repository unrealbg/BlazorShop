namespace BlazorShop.Storefront.Services
{
    using System.Globalization;
    using System.Text;
    using System.Xml;
    using System.Xml.Linq;

    using BlazorShop.Web.Shared.Models.Discovery;

    using BlazorShop.Storefront.Services.Contracts;

    public class StorefrontSitemapService : IStorefrontSitemapService
    {
        private readonly StorefrontApiClient _apiClient;
        private readonly IStorefrontPublicUrlResolver _publicUrlResolver;
        private readonly IStorefrontSeoSettingsProvider _seoSettingsProvider;

        public StorefrontSitemapService(
            StorefrontApiClient apiClient,
            IStorefrontPublicUrlResolver publicUrlResolver,
            IStorefrontSeoSettingsProvider seoSettingsProvider)
        {
            _apiClient = apiClient;
            _publicUrlResolver = publicUrlResolver;
            _seoSettingsProvider = seoSettingsProvider;
        }

        public async Task<StorefrontSitemapGenerationResult> GenerateAsync(CancellationToken cancellationToken = default)
        {
            var settingsTask = _seoSettingsProvider.GetAsync(cancellationToken);
            var sitemapTask = _apiClient.GetPublishedSitemapAsync(cancellationToken);

            await Task.WhenAll(settingsTask, sitemapTask);

            var sitemapResult = sitemapTask.Result;
            if (sitemapResult.IsServiceUnavailable)
            {
                return StorefrontSitemapGenerationResult.ServiceUnavailable();
            }

            var configuredBaseUrl = settingsTask.Result?.BaseCanonicalUrl;
            var urls = BuildEntries(sitemapResult.Value ?? new GetPublicCatalogSitemap(), configuredBaseUrl);
            return StorefrontSitemapGenerationResult.Success(Serialize(urls));
        }

        private IReadOnlyList<SitemapUrlEntry> BuildEntries(GetPublicCatalogSitemap sitemap, string? configuredBaseUrl)
        {
            var catalogLastModifiedUtc = sitemap.Products
                .Where(product => product.LastModifiedUtc.HasValue)
                .Select(product => product.LastModifiedUtc)
                .OrderByDescending(lastModifiedUtc => lastModifiedUtc)
                .FirstOrDefault();

            var entries = new List<SitemapUrlEntry>();

            entries.AddRange(StorefrontRoutes.SitemapStaticPages
                .Select(route => new SitemapUrlEntry(
                    _publicUrlResolver.ResolveAbsoluteUrl(route.Path, configuredBaseUrl),
                    route.UseCatalogLastModified ? catalogLastModifiedUtc : null)));

            entries.AddRange(sitemap.Categories
                .Where(category => !string.IsNullOrWhiteSpace(category.Slug))
                .Select(category => new SitemapUrlEntry(
                    _publicUrlResolver.ResolveAbsoluteUrl(StorefrontRoutes.Category(category.Slug), configuredBaseUrl),
                    category.LastModifiedUtc)));

            entries.AddRange(sitemap.Products
                .Where(product => !string.IsNullOrWhiteSpace(product.Slug))
                .Select(product => new SitemapUrlEntry(
                    _publicUrlResolver.ResolveAbsoluteUrl(StorefrontRoutes.Product(product.Slug), configuredBaseUrl),
                    product.LastModifiedUtc)));

            return entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Location))
                .GroupBy(entry => entry.Location!, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(entry => entry.LastModifiedUtc)
                    .First())
                .ToList();
        }

        private static string Serialize(IReadOnlyList<SitemapUrlEntry> entries)
        {
            XNamespace sitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";
            var document = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement(
                    sitemapNamespace + "urlset",
                    entries.Select(entry => CreateUrlElement(entry, sitemapNamespace))));

            using var stringWriter = new Utf8StringWriter();
            using var xmlWriter = XmlWriter.Create(
                stringWriter,
                new XmlWriterSettings
                {
                    Encoding = Encoding.UTF8,
                    Indent = true,
                    OmitXmlDeclaration = false,
                });

            document.Save(xmlWriter);
            xmlWriter.Flush();

            return stringWriter.ToString();
        }

        private static XElement CreateUrlElement(SitemapUrlEntry entry, XNamespace sitemapNamespace)
        {
            var urlElement = new XElement(sitemapNamespace + "url", new XElement(sitemapNamespace + "loc", entry.Location));

            if (entry.LastModifiedUtc.HasValue)
            {
                urlElement.Add(new XElement(
                    sitemapNamespace + "lastmod",
                    entry.LastModifiedUtc.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)));
            }

            return urlElement;
        }

        private sealed record SitemapUrlEntry(string? Location, DateTime? LastModifiedUtc);

        private sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }
    }
}