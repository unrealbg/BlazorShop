namespace BlazorShop.Storefront.Services
{
    using BlazorShop.Storefront.Options;
    using BlazorShop.Storefront.Services.Contracts;

    using Microsoft.Extensions.Options;

    public class StorefrontPublicUrlResolver : IStorefrontPublicUrlResolver
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IOptions<StorefrontPublicUrlOptions> _options;

        public StorefrontPublicUrlResolver(IHttpContextAccessor httpContextAccessor, IOptions<StorefrontPublicUrlOptions> options)
        {
            _httpContextAccessor = httpContextAccessor;
            _options = options;
        }

        public string? ResolveBaseUrl(string? configuredBaseUrl = null)
        {
            return NormalizeBaseUrl(_options.Value.BaseUrl)
                ?? NormalizeBaseUrl(configuredBaseUrl)
                ?? ResolveRequestBaseUrl();
        }

        public string? ResolveAbsoluteUrl(string? relativeOrAbsoluteUrl, string? configuredBaseUrl = null)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsoluteUrl))
            {
                return null;
            }

            if (Uri.TryCreate(relativeOrAbsoluteUrl.Trim(), UriKind.Absolute, out var absoluteUri)
                && IsSupportedAbsoluteUri(absoluteUri))
            {
                return absoluteUri.ToString();
            }

            var baseUrl = ResolveBaseUrl(configuredBaseUrl);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return null;
            }

            var relativePath = relativeOrAbsoluteUrl.StartsWith("/", StringComparison.Ordinal)
                ? relativeOrAbsoluteUrl.Trim()
                : $"/{relativeOrAbsoluteUrl.TrimStart('/')}";

            return new Uri(new Uri(baseUrl, UriKind.Absolute), relativePath).ToString();
        }

        private string? ResolveRequestBaseUrl()
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            if (request is null || !request.Host.HasValue)
            {
                return null;
            }

            var uriBuilder = new UriBuilder(request.Scheme, request.Host.Host)
            {
                Port = request.Host.Port ?? -1,
                Path = EnsureTrailingSlash(request.PathBase.HasValue ? request.PathBase.Value : "/"),
            };

            return uriBuilder.Uri.ToString();
        }

        private static string? NormalizeBaseUrl(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate)
                || !Uri.TryCreate(candidate.Trim(), UriKind.Absolute, out var absoluteUri)
                || !IsSupportedAbsoluteUri(absoluteUri))
            {
                return null;
            }

            var uriBuilder = new UriBuilder(absoluteUri)
            {
                Fragment = string.Empty,
                Query = string.Empty,
                Path = EnsureTrailingSlash(absoluteUri.AbsolutePath),
            };

            return uriBuilder.Uri.ToString();
        }

        private static string EnsureTrailingSlash(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return "/";
            }

            return path.EndsWith("/", StringComparison.Ordinal)
                ? path
                : $"{path}/";
        }

        private static bool IsSupportedAbsoluteUri(Uri uri)
        {
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }
    }
}