namespace BlazorShop.Web.Services
{
    using BlazorShop.Web.Services.Contracts;

    public class StorefrontSeoMetadataBuilder : IStorefrontSeoMetadataBuilder
    {
        public StorefrontSeoMetadata Build(StorefrontSeoMetadataBuildRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var title = AppendTitleSuffix(
                FirstNonEmpty(request.PageSeo?.MetaTitle, request.PageTitle, request.Settings?.SiteName),
                request.Settings?.DefaultTitleSuffix);
            var metaDescription = FirstNonEmpty(
                request.PageSeo?.MetaDescription,
                request.FallbackMetaDescription,
                request.Settings?.DefaultMetaDescription);

            return new StorefrontSeoMetadata
            {
                Title = title,
                MetaDescription = metaDescription,
                CanonicalUrl = ResolveCanonicalUrl(request.PageSeo?.CanonicalUrl, request.Settings?.BaseCanonicalUrl, request.RelativePath),
                OgTitle = FirstNonEmpty(request.PageSeo?.OgTitle, title),
                OgDescription = FirstNonEmpty(request.PageSeo?.OgDescription, metaDescription),
                OgImage = ResolveContentUrl(
                    FirstNonEmpty(request.PageSeo?.OgImage, request.FallbackOgImage, request.Settings?.DefaultOgImage),
                    request.Settings?.BaseCanonicalUrl),
                SiteName = request.Settings?.SiteName,
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