namespace BlazorShop.Application.Services
{
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;

    public class SeoMetadataBuilder : ISeoMetadataBuilder
    {
        public SeoMetadataDto Build(SeoMetadataBuildRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var title = AppendTitleSuffix(
                FirstNonEmpty(request.PageSeo?.MetaTitle, request.PageTitle, request.Settings?.SiteName),
                request.Settings?.DefaultTitleSuffix);
            var metaDescription = FirstNonEmpty(request.PageSeo?.MetaDescription, request.Settings?.DefaultMetaDescription);
            var canonicalUrl = request.SuppressCanonicalUrl
                ? null
                : ResolveCanonicalUrl(request.PageSeo?.CanonicalUrl, request.Settings?.BaseCanonicalUrl, request.RelativePath);
            var suppressOpenGraph = request.SuppressOpenGraph;

            return new SeoMetadataDto
            {
                Title = title,
                MetaDescription = metaDescription,
                CanonicalUrl = canonicalUrl,
                OgTitle = suppressOpenGraph ? null : FirstNonEmpty(request.PageSeo?.OgTitle, title),
                OgDescription = suppressOpenGraph ? null : FirstNonEmpty(request.PageSeo?.OgDescription, metaDescription),
                OgImage = suppressOpenGraph ? null : ResolveContentUrl(FirstNonEmpty(request.PageSeo?.OgImage, request.Settings?.DefaultOgImage), request.Settings?.BaseCanonicalUrl),
                SiteName = suppressOpenGraph ? null : request.Settings?.SiteName,
                RobotsIndex = request.PageSeo?.RobotsIndex ?? true,
                RobotsFollow = request.PageSeo?.RobotsFollow ?? true,
            };
        }

        private static string? AppendTitleSuffix(string? title, string? suffix)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.IsNullOrWhiteSpace(suffix) ? null : suffix.Trim();
            }

            if (string.IsNullOrWhiteSpace(suffix))
            {
                return title.Trim();
            }

            var normalizedTitle = title.Trim();
            var normalizedSuffix = suffix.Trim();

            return normalizedTitle.EndsWith(normalizedSuffix, StringComparison.OrdinalIgnoreCase)
                ? normalizedTitle
                : $"{normalizedTitle} {normalizedSuffix}";
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
        }

        private static string? ResolveCanonicalUrl(string? canonicalUrl, string? baseCanonicalUrl, string? relativePath)
        {
            if (!string.IsNullOrWhiteSpace(canonicalUrl))
            {
                return ResolveContentUrl(canonicalUrl, baseCanonicalUrl);
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return null;
            }

            return ResolveContentUrl(relativePath, baseCanonicalUrl);
        }

        private static string? ResolveContentUrl(string? value, string? baseCanonicalUrl)
        {
            return TryCombineAbsoluteUrl(baseCanonicalUrl, value) ?? value?.Trim();
        }

        private static string? TryCombineAbsoluteUrl(string? baseCanonicalUrl, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteValue)
                && IsSupportedAbsoluteUri(absoluteValue))
            {
                return absoluteValue.ToString();
            }

            if (string.IsNullOrWhiteSpace(baseCanonicalUrl)
                || !Uri.TryCreate(baseCanonicalUrl, UriKind.Absolute, out var baseUri)
                || !IsSupportedAbsoluteUri(baseUri)
                || !value.StartsWith("/", StringComparison.Ordinal))
            {
                return null;
            }

            return new Uri(baseUri, value).ToString();
        }

        private static bool IsSupportedAbsoluteUri(Uri uri)
        {
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }
    }
}