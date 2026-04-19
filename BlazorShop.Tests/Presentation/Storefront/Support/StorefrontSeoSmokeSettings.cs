namespace BlazorShop.Tests.Presentation.Storefront
{
    using BlazorShop.Storefront.Services;

    internal sealed class StorefrontSeoSmokeSettings
    {
        public const string AllowInvalidCertificateEnvironmentVariableName = "BLAZORSHOP_SEO_SMOKE_ALLOW_INVALID_CERTIFICATE";
        public const string BaseUrlEnvironmentVariableName = "BLAZORSHOP_SEO_SMOKE_BASE_URL";
        public const string CategoryPathEnvironmentVariableName = "BLAZORSHOP_SEO_SMOKE_CATEGORY_PATH";
        public const string MissingPathEnvironmentVariableName = "BLAZORSHOP_SEO_SMOKE_MISSING_PATH";
        public const string ProductPathEnvironmentVariableName = "BLAZORSHOP_SEO_SMOKE_PRODUCT_PATH";
        public const string RequireConfigurationEnvironmentVariableName = "BLAZORSHOP_SEO_SMOKE_REQUIRE_CONFIGURATION";
        public const string RedirectExpectedStatusCodeEnvironmentVariableName = "BLAZORSHOP_SEO_SMOKE_REDIRECT_STATUS_CODE";
        public const string RedirectSourcePathEnvironmentVariableName = "BLAZORSHOP_SEO_SMOKE_REDIRECT_SOURCE_PATH";
        public const string RedirectTargetPathEnvironmentVariableName = "BLAZORSHOP_SEO_SMOKE_REDIRECT_TARGET_PATH";
        public const string StaticPagePathEnvironmentVariableName = "BLAZORSHOP_SEO_SMOKE_STATIC_PATH";

        private const int DefaultRedirectStatusCode = 301;
        private const string DefaultRedirectSourcePath = "/product/legacy-runner";
        private const string DefaultRedirectTargetPath = "/product/metro-runner";

        private StorefrontSeoSmokeSettings(
            bool isEnabled,
            Uri baseUri,
            bool allowInvalidCertificate,
            bool requireConfiguration,
            string staticPagePath,
            string categoryPath,
            string productPath,
            string missingPath,
            string? redirectSourcePath,
            string? redirectTargetPath,
            int redirectExpectedStatusCode)
        {
            IsEnabled = isEnabled;
            BaseUri = baseUri;
            AllowInvalidCertificate = allowInvalidCertificate;
            RequireConfiguration = requireConfiguration;
            HomePath = StorefrontRoutes.Home;
            StaticPagePath = staticPagePath;
            CategoryPath = categoryPath;
            ProductPath = productPath;
            MissingPath = missingPath;
            RedirectSourcePath = redirectSourcePath;
            RedirectTargetPath = redirectTargetPath;
            RedirectExpectedStatusCode = redirectExpectedStatusCode;
        }

        public bool AllowInvalidCertificate { get; }

        public Uri BaseUri { get; }

        public string CategoryPath { get; }

        public string HomePath { get; }

        public bool HasRedirectExpectation => !string.IsNullOrWhiteSpace(RedirectSourcePath)
            && !string.IsNullOrWhiteSpace(RedirectTargetPath);

        public bool IsEnabled { get; }

        public string MissingPath { get; }

        public string ProductPath { get; }

        public bool RequireConfiguration { get; }

        public int RedirectExpectedStatusCode { get; }

        public string? RedirectSourcePath { get; }

        public string? RedirectTargetPath { get; }

        public string StaticPagePath { get; }

        public static StorefrontSeoSmokeSettings FromEnvironment()
        {
            return FromLookup(Environment.GetEnvironmentVariable);
        }

        internal static StorefrontSeoSmokeSettings FromLookup(Func<string, string?> getValue)
        {
            var requireConfiguration = ParseBoolean(getValue(RequireConfigurationEnvironmentVariableName), RequireConfigurationEnvironmentVariableName);
            var baseUrl = getValue(BaseUrlEnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return new StorefrontSeoSmokeSettings(
                    isEnabled: false,
                    baseUri: new Uri("http://localhost/", UriKind.Absolute),
                    allowInvalidCertificate: false,
                    requireConfiguration: requireConfiguration,
                    staticPagePath: StorefrontRoutes.About,
                    categoryPath: StorefrontRoutes.Category("sneakers"),
                    productPath: StorefrontRoutes.Product("metro-runner"),
                    missingPath: StorefrontRoutes.Product("missing-product"),
                    redirectSourcePath: DefaultRedirectSourcePath,
                    redirectTargetPath: DefaultRedirectTargetPath,
                    redirectExpectedStatusCode: DefaultRedirectStatusCode);
            }

            var normalizedBaseUri = NormalizeBaseUri(baseUrl);
            var allowInvalidCertificate = ParseBoolean(getValue(AllowInvalidCertificateEnvironmentVariableName), AllowInvalidCertificateEnvironmentVariableName);
            var staticPagePath = ReadRequiredRoutePath(getValue, StaticPagePathEnvironmentVariableName, StorefrontRoutes.About);
            var categoryPath = ReadRequiredRoutePath(getValue, CategoryPathEnvironmentVariableName, StorefrontRoutes.Category("sneakers"));
            var productPath = ReadRequiredRoutePath(getValue, ProductPathEnvironmentVariableName, StorefrontRoutes.Product("metro-runner"));
            var missingPath = ReadRequiredRoutePath(getValue, MissingPathEnvironmentVariableName, StorefrontRoutes.Product("missing-product"));
            var redirectSourcePath = ReadOptionalRoutePath(getValue, RedirectSourcePathEnvironmentVariableName, DefaultRedirectSourcePath);
            var redirectTargetPath = ReadOptionalRouteOrAbsoluteUrl(getValue, RedirectTargetPathEnvironmentVariableName, DefaultRedirectTargetPath);
            var redirectExpectedStatusCode = ReadRedirectStatusCode(getValue);

            if (string.IsNullOrWhiteSpace(redirectSourcePath) != string.IsNullOrWhiteSpace(redirectTargetPath))
            {
                throw new InvalidOperationException($"Set both {RedirectSourcePathEnvironmentVariableName} and {RedirectTargetPathEnvironmentVariableName}, or leave both unset/blank to disable the redirect smoke check.");
            }

            return new StorefrontSeoSmokeSettings(
                isEnabled: true,
                baseUri: normalizedBaseUri,
                allowInvalidCertificate: allowInvalidCertificate,
                requireConfiguration: requireConfiguration,
                staticPagePath: staticPagePath,
                categoryPath: categoryPath,
                productPath: productPath,
                missingPath: missingPath,
                redirectSourcePath: redirectSourcePath,
                redirectTargetPath: redirectTargetPath,
                redirectExpectedStatusCode: redirectExpectedStatusCode);
        }

        public string ResolveAbsoluteUrl(string routePath)
        {
            EnsureEnabled();

            var normalizedRoutePath = NormalizeRoutePath(routePath, nameof(routePath));
            var relativeTarget = normalizedRoutePath == StorefrontRoutes.Home
                ? string.Empty
                : normalizedRoutePath.TrimStart('/');

            return new Uri(BaseUri, relativeTarget).ToString();
        }

        public string ResolveExpectedCanonicalUrl(string routePathOrAbsoluteUrl)
        {
            EnsureEnabled();

            if (Uri.TryCreate(routePathOrAbsoluteUrl, UriKind.Absolute, out var absoluteUri))
            {
                if (absoluteUri.Scheme != Uri.UriSchemeHttp && absoluteUri.Scheme != Uri.UriSchemeHttps)
                {
                    throw new InvalidOperationException($"{RedirectTargetPathEnvironmentVariableName} must use http or https when configured as an absolute URL.");
                }

                return absoluteUri.ToString();
            }

            return ResolveAbsoluteUrl(routePathOrAbsoluteUrl);
        }

        public string ToRequestTarget(string routePathOrAbsoluteUrl)
        {
            EnsureEnabled();

            if (Uri.TryCreate(routePathOrAbsoluteUrl, UriKind.Absolute, out var absoluteUri))
            {
                return absoluteUri.ToString();
            }

            var normalizedRoutePath = NormalizeRoutePath(routePathOrAbsoluteUrl, nameof(routePathOrAbsoluteUrl));
            return normalizedRoutePath == StorefrontRoutes.Home
                ? string.Empty
                : normalizedRoutePath.TrimStart('/');
        }

        private void EnsureEnabled()
        {
            if (!IsEnabled)
            {
                throw new InvalidOperationException($"Set {BaseUrlEnvironmentVariableName} to enable the storefront SEO smoke suite.");
            }
        }

        private static bool ParseBoolean(string? rawValue, string environmentVariableName)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return false;
            }

            return rawValue.Trim().ToLowerInvariant() switch
            {
                "1" or "true" or "yes" => true,
                "0" or "false" or "no" => false,
                _ => throw new InvalidOperationException($"{environmentVariableName} must be true/false, yes/no, or 1/0 when specified."),
            };
        }

        private static int ReadRedirectStatusCode(Func<string, string?> getValue)
        {
            var rawValue = getValue(RedirectExpectedStatusCodeEnvironmentVariableName);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return DefaultRedirectStatusCode;
            }

            if (!int.TryParse(rawValue.Trim(), out var statusCode) || statusCode < 300 || statusCode > 399)
            {
                throw new InvalidOperationException($"{RedirectExpectedStatusCodeEnvironmentVariableName} must be a valid 3xx status code when specified.");
            }

            return statusCode;
        }

        private static string? ReadOptionalRouteOrAbsoluteUrl(Func<string, string?> getValue, string environmentVariableName, string defaultValue)
        {
            var rawValue = getValue(environmentVariableName);
            if (rawValue is null)
            {
                return defaultValue;
            }

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            var trimmedValue = rawValue.Trim();
            if (Uri.TryCreate(trimmedValue, UriKind.Absolute, out var absoluteUri))
            {
                if (absoluteUri.Scheme != Uri.UriSchemeHttp && absoluteUri.Scheme != Uri.UriSchemeHttps)
                {
                    throw new InvalidOperationException($"{environmentVariableName} must use http or https when specified as an absolute URL.");
                }

                return absoluteUri.ToString();
            }

            return NormalizeRoutePath(trimmedValue, environmentVariableName);
        }

        private static string? ReadOptionalRoutePath(Func<string, string?> getValue, string environmentVariableName, string defaultValue)
        {
            var rawValue = getValue(environmentVariableName);
            if (rawValue is null)
            {
                return defaultValue;
            }

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            return NormalizeRoutePath(rawValue, environmentVariableName);
        }

        private static string ReadRequiredRoutePath(Func<string, string?> getValue, string environmentVariableName, string defaultValue)
        {
            var rawValue = getValue(environmentVariableName);
            if (rawValue is null)
            {
                return defaultValue;
            }

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                throw new InvalidOperationException($"{environmentVariableName} cannot be blank when specified.");
            }

            return NormalizeRoutePath(rawValue, environmentVariableName);
        }

        private static Uri NormalizeBaseUri(string rawBaseUrl)
        {
            if (!Uri.TryCreate(rawBaseUrl.Trim(), UriKind.Absolute, out var baseUri))
            {
                throw new InvalidOperationException($"{BaseUrlEnvironmentVariableName} must be an absolute http or https URL.");
            }

            if (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
            {
                throw new InvalidOperationException($"{BaseUrlEnvironmentVariableName} must use http or https.");
            }

            var builder = new UriBuilder(baseUri)
            {
                Fragment = string.Empty,
                Query = string.Empty,
            };

            builder.Path = string.IsNullOrWhiteSpace(builder.Path)
                ? "/"
                : builder.Path.EndsWith("/", StringComparison.Ordinal)
                    ? builder.Path
                    : $"{builder.Path}/";

            return builder.Uri;
        }

        private static string NormalizeRoutePath(string rawRoutePath, string environmentVariableName)
        {
            var trimmedValue = rawRoutePath.Trim();
            if (trimmedValue.Length == 0)
            {
                throw new InvalidOperationException($"{environmentVariableName} must be a non-empty storefront route path.");
            }

            if (Uri.TryCreate(trimmedValue, UriKind.Absolute, out _))
            {
                throw new InvalidOperationException($"{environmentVariableName} must be a storefront route path, not an absolute URL.");
            }

            if (!trimmedValue.StartsWith("/", StringComparison.Ordinal))
            {
                trimmedValue = $"/{trimmedValue}";
            }

            return trimmedValue.Length == 1
                ? StorefrontRoutes.Home
                : trimmedValue.TrimEnd('/');
        }
    }
}