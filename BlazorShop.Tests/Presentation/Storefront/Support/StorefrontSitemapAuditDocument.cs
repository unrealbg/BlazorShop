namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Globalization;
    using System.Xml.Linq;

    internal sealed class StorefrontSitemapAuditDocument
    {
        private readonly XNamespace _sitemapNamespace;

        private StorefrontSitemapAuditDocument(XDocument document)
        {
            Document = document;
            _sitemapNamespace = document.Root?.Name.Namespace ?? XNamespace.None;
        }

        public XDocument Document { get; }

        public IReadOnlyDictionary<string, DateTimeOffset> LastModifiedByLocation => UrlElements
            .Select(element => new
            {
                Location = element.Element(_sitemapNamespace + "loc")?.Value,
                LastModified = element.Element(_sitemapNamespace + "lastmod")?.Value,
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Location) && !string.IsNullOrWhiteSpace(item.LastModified))
            .ToDictionary(
                item => item.Location!,
                item => DateTimeOffset.Parse(item.LastModified!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal),
                StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> Locations => UrlElements
            .Select(element => element.Element(_sitemapNamespace + "loc")?.Value)
            .Where(location => !string.IsNullOrWhiteSpace(location))
            .Select(location => location!)
            .ToArray();

        public bool LocationsAreAbsolute => Locations.All(location => Uri.TryCreate(location, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps));

        public static async Task<StorefrontSitemapAuditDocument> CreateAsync(HttpResponseMessage response)
        {
            var xml = await response.Content.ReadAsStringAsync();
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            return new StorefrontSitemapAuditDocument(document);
        }

        public DateTimeOffset? GetLastModified(string location)
        {
            return LastModifiedByLocation.TryGetValue(location, out var lastModified)
                ? lastModified
                : null;
        }

        private IEnumerable<XElement> UrlElements => Document.Root?.Elements(_sitemapNamespace + "url") ?? Enumerable.Empty<XElement>();
    }
}