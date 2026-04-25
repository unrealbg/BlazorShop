namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;
    using System.Text.Json.Nodes;
    using System.Text.RegularExpressions;

    internal sealed class StorefrontHtmlAuditDocument
    {
        private static readonly Regex AttributeRegex = new("(?<name>[\\w:-]+)\\s*=\\s*(?<quote>['\"])(?<value>.*?)\\k<quote>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

        private readonly IReadOnlyList<IReadOnlyDictionary<string, string>> _anchorTags;
        private readonly IReadOnlyList<IReadOnlyDictionary<string, string>> _imageTags;
        private readonly IReadOnlyList<IReadOnlyDictionary<string, string>> _linkTags;
        private readonly IReadOnlyList<IReadOnlyDictionary<string, string>> _metaTags;
        private readonly IReadOnlyList<IReadOnlyDictionary<string, string>> _scriptTags;

        private StorefrontHtmlAuditDocument(string html)
        {
            _linkTags = ExtractStartTags(html, "link");
            _metaTags = ExtractStartTags(html, "meta");
            _anchorTags = ExtractStartTags(html, "a");
            _imageTags = ExtractStartTags(html, "img");
            _scriptTags = ExtractStartTags(html, "script");
            JsonLdBlocks = ExtractJsonLdBlocks(html);
        }

        public IReadOnlyList<string> AssetUrls => EnumerateAssetUrls()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        public IReadOnlyList<string> AnchorHrefs => _anchorTags
            .Select(tag => GetAttribute(tag, "href"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        public IReadOnlyList<string> BrokenAssetUrls => AssetUrls
            .Where(IsBrokenHref)
            .ToArray();

        public IReadOnlyList<string> BrokenAnchorHrefs => AnchorHrefs
            .Where(IsBrokenHref)
            .ToArray();

        public IReadOnlyList<string> CanonicalUrls => _linkTags
            .Where(tag => string.Equals(GetAttribute(tag, "rel"), "canonical", StringComparison.OrdinalIgnoreCase))
            .Select(tag => GetAttribute(tag, "href"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

        public bool HasAnyOpenGraphTags => OpenGraphProperties.Count > 0;

        public IReadOnlyList<string> InternalAnchorHrefs => AnchorHrefs
            .Where(value => value.StartsWith("/", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        public IReadOnlyList<JsonNode> JsonLdBlocks { get; }

        public IReadOnlyDictionary<string, string> OpenGraphProperties => _metaTags
            .Where(tag => GetAttribute(tag, "property")?.StartsWith("og:", StringComparison.OrdinalIgnoreCase) == true)
            .GroupBy(tag => GetAttribute(tag, "property")!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => GetAttribute(group.Last(), "content") ?? string.Empty, StringComparer.OrdinalIgnoreCase);

        public string? RobotsMetaContent => _metaTags
            .Where(tag => string.Equals(GetAttribute(tag, "name"), "robots", StringComparison.OrdinalIgnoreCase))
            .Select(tag => GetAttribute(tag, "content"))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        public IReadOnlyCollection<string> SchemaTypes => JsonLdBlocks
            .SelectMany(EnumerateSchemaTypes)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        public static StorefrontHtmlAuditDocument Create(string html)
        {
            return new StorefrontHtmlAuditDocument(html);
        }

        public static async Task<StorefrontHtmlAuditDocument> CreateAsync(HttpResponseMessage response)
        {
            var html = await response.Content.ReadAsStringAsync();
            return Create(html);
        }

        public bool ContainsJsonProperty(string propertyName)
        {
            return JsonLdBlocks.Any(block => ContainsJsonProperty(block, propertyName));
        }

        public string? GetOpenGraphProperty(string propertyName)
        {
            return OpenGraphProperties.TryGetValue(propertyName, out var value)
                ? value
                : null;
        }

        public bool HasSchemaType(string schemaType)
        {
            return SchemaTypes.Contains(schemaType, StringComparer.Ordinal);
        }

        private static bool ContainsJsonProperty(JsonNode? node, string propertyName)
        {
            return node switch
            {
                JsonObject obj => obj.Any(property => string.Equals(property.Key, propertyName, StringComparison.Ordinal) || ContainsJsonProperty(property.Value, propertyName)),
                JsonArray array => array.Any(item => ContainsJsonProperty(item, propertyName)),
                _ => false,
            };
        }

        private static IEnumerable<string> EnumerateSchemaTypes(JsonNode? node)
        {
            switch (node)
            {
                case JsonObject obj:
                    if (obj["@type"] is JsonValue typeValue && typeValue.TryGetValue<string>(out var schemaType) && !string.IsNullOrWhiteSpace(schemaType))
                    {
                        yield return schemaType;
                    }
                    else if (obj["@type"] is JsonArray typeArray)
                    {
                        foreach (var value in typeArray.OfType<JsonValue>())
                        {
                            if (value.TryGetValue<string>(out var arrayType) && !string.IsNullOrWhiteSpace(arrayType))
                            {
                                yield return arrayType;
                            }
                        }
                    }

                    foreach (var child in obj.Select(property => property.Value))
                    {
                        foreach (var childType in EnumerateSchemaTypes(child))
                        {
                            yield return childType;
                        }
                    }

                    break;

                case JsonArray array:
                    foreach (var item in array)
                    {
                        foreach (var childType in EnumerateSchemaTypes(item))
                        {
                            yield return childType;
                        }
                    }

                    break;
            }
        }

        private IEnumerable<string> EnumerateAssetUrls()
        {
            foreach (var value in _imageTags
                .Select(tag => GetAttribute(tag, "src"))
                .Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                yield return value!;
            }

            foreach (var value in _scriptTags
                .Select(tag => GetAttribute(tag, "src"))
                .Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                yield return value!;
            }

            foreach (var value in _linkTags
                .Where(tag => !string.Equals(GetAttribute(tag, "rel"), "canonical", StringComparison.OrdinalIgnoreCase))
                .Select(tag => GetAttribute(tag, "href"))
                .Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                yield return value!;
            }
        }

        private static IReadOnlyList<IReadOnlyDictionary<string, string>> ExtractStartTags(string html, string tagName)
        {
            var regex = new Regex($@"<{Regex.Escape(tagName)}\b(?<attributes>[^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            return regex.Matches(html)
                .Select(match => ParseAttributes(match.Groups["attributes"].Value))
                .ToArray();
        }

        private static IReadOnlyList<JsonNode> ExtractJsonLdBlocks(string html)
        {
            var regex = new Regex(@"<script\b(?<attributes>[^>]*)>(?<content>.*?)</script>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
            var documents = new List<JsonNode>();

            foreach (Match match in regex.Matches(html))
            {
                var attributes = ParseAttributes(match.Groups["attributes"].Value);
                if (!string.Equals(GetAttribute(attributes, "type"), "application/ld+json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var json = WebUtility.HtmlDecode(match.Groups["content"].Value).Trim();
                if (string.IsNullOrWhiteSpace(json))
                {
                    continue;
                }

                var document = JsonNode.Parse(json);
                if (document is not null)
                {
                    documents.Add(document);
                }
            }

            return documents;
        }

        private static string? GetAttribute(IReadOnlyDictionary<string, string> attributes, string name)
        {
            return attributes.TryGetValue(name, out var value)
                ? value
                : null;
        }

        private static bool IsBrokenHref(string href)
        {
            return string.IsNullOrWhiteSpace(href)
                || string.Equals(href, "#", StringComparison.Ordinal)
                || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
                || href.Contains("undefined", StringComparison.OrdinalIgnoreCase)
                || href.Contains("null", StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyDictionary<string, string> ParseAttributes(string attributeBlock)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in AttributeRegex.Matches(attributeBlock))
            {
                attributes[match.Groups["name"].Value] = WebUtility.HtmlDecode(match.Groups["value"].Value);
            }

            return attributes;
        }
    }
}