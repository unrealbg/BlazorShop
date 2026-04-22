namespace BlazorShop.Storefront.Services
{
    using BlazorShop.Application.Options;
    using BlazorShop.Storefront.Services.Contracts;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Options;

    public class StorefrontClientAppUrlResolver : IStorefrontClientAppUrlResolver
    {
        private readonly IConfiguration _configuration;
        private readonly IOptions<ClientAppOptions> _options;

        public StorefrontClientAppUrlResolver(IConfiguration configuration, IOptions<ClientAppOptions> options)
        {
            _configuration = configuration;
            _options = options;
        }

        public string? ResolveBaseUrl()
        {
            return NormalizeBaseUrl(_configuration["Services:adminclient:https:0"])
                ?? NormalizeBaseUrl(_configuration["Services:adminclient:http:0"])
                ?? NormalizeBaseUrl(_options.Value.BaseUrl);
        }

        public string ResolveUrl(string? relativeOrAbsoluteUrl)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsoluteUrl))
            {
                return "/";
            }

            if (Uri.TryCreate(relativeOrAbsoluteUrl.Trim(), UriKind.Absolute, out var absoluteUri)
                && IsSupportedAbsoluteUri(absoluteUri))
            {
                return absoluteUri.ToString();
            }

            var relativePath = relativeOrAbsoluteUrl.StartsWith("/", StringComparison.Ordinal)
                ? relativeOrAbsoluteUrl.Trim()
                : $"/{relativeOrAbsoluteUrl.TrimStart('/')}";

            var baseUrl = ResolveBaseUrl();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return relativePath;
            }

            return new Uri(new Uri(baseUrl, UriKind.Absolute), relativePath).ToString();
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