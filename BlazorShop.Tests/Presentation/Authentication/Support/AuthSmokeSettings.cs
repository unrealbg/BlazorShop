namespace BlazorShop.Tests.Presentation.Authentication
{
    internal sealed class AuthSmokeSettings
    {
        public const string AllowInvalidCertificateEnvironmentVariableName = "BLAZORSHOP_AUTH_SMOKE_ALLOW_INVALID_CERTIFICATE";
        public const string ApiBaseUrlEnvironmentVariableName = "BLAZORSHOP_AUTH_SMOKE_API_BASE_URL";
        public const string ClientAppBaseUrlEnvironmentVariableName = "BLAZORSHOP_AUTH_SMOKE_CLIENT_APP_BASE_URL";
        public const string RefreshCookieNameEnvironmentVariableName = "BLAZORSHOP_AUTH_SMOKE_REFRESH_COOKIE_NAME";
        public const string RequireConfigurationEnvironmentVariableName = "BLAZORSHOP_AUTH_SMOKE_REQUIRE_CONFIGURATION";
        public const string StorefrontBaseUrlEnvironmentVariableName = "BLAZORSHOP_AUTH_SMOKE_STOREFRONT_BASE_URL";

        private const string DefaultRefreshCookieName = "__Host-blazorshop-refresh";

        private AuthSmokeSettings(
            bool isEnabled,
            Uri authenticationBaseUri,
            Uri storefrontBaseUri,
            Uri clientAppBaseUri,
            bool allowInvalidCertificate,
            bool requireConfiguration,
            string refreshCookieName)
        {
            IsEnabled = isEnabled;
            AuthenticationBaseUri = authenticationBaseUri;
            StorefrontBaseUri = storefrontBaseUri;
            ClientAppBaseUri = clientAppBaseUri;
            AllowInvalidCertificate = allowInvalidCertificate;
            RequireConfiguration = requireConfiguration;
            RefreshCookieName = refreshCookieName;
        }

        public bool AllowInvalidCertificate { get; }

        public Uri AuthenticationBaseUri { get; }

        public Uri ClientAppBaseUri { get; }

        public bool IsEnabled { get; }

        public string RefreshCookieName { get; }

        public bool RequireConfiguration { get; }

        public Uri StorefrontBaseUri { get; }

        public static AuthSmokeSettings FromEnvironment()
        {
            return FromLookup(Environment.GetEnvironmentVariable);
        }

        internal static AuthSmokeSettings FromLookup(Func<string, string?> getValue)
        {
            var requireConfiguration = ParseBoolean(getValue(RequireConfigurationEnvironmentVariableName), RequireConfigurationEnvironmentVariableName);
            var apiBaseUrl = getValue(ApiBaseUrlEnvironmentVariableName);
            var storefrontBaseUrl = getValue(StorefrontBaseUrlEnvironmentVariableName);
            var clientAppBaseUrl = getValue(ClientAppBaseUrlEnvironmentVariableName);

            if (AreAllBlank(apiBaseUrl, storefrontBaseUrl, clientAppBaseUrl))
            {
                return new AuthSmokeSettings(
                    isEnabled: false,
                    authenticationBaseUri: new Uri("http://localhost/api/authentication/", UriKind.Absolute),
                    storefrontBaseUri: new Uri("http://localhost/", UriKind.Absolute),
                    clientAppBaseUri: new Uri("http://localhost/", UriKind.Absolute),
                    allowInvalidCertificate: false,
                    requireConfiguration: requireConfiguration,
                    refreshCookieName: DefaultRefreshCookieName);
            }

            if (string.IsNullOrWhiteSpace(apiBaseUrl)
                || string.IsNullOrWhiteSpace(storefrontBaseUrl)
                || string.IsNullOrWhiteSpace(clientAppBaseUrl))
            {
                throw new InvalidOperationException($"Set {ApiBaseUrlEnvironmentVariableName}, {StorefrontBaseUrlEnvironmentVariableName}, and {ClientAppBaseUrlEnvironmentVariableName} together to enable the auth smoke suite.");
            }

            var allowInvalidCertificate = ParseBoolean(getValue(AllowInvalidCertificateEnvironmentVariableName), AllowInvalidCertificateEnvironmentVariableName);
            var refreshCookieName = getValue(RefreshCookieNameEnvironmentVariableName);

            return new AuthSmokeSettings(
                isEnabled: true,
                authenticationBaseUri: NormalizeAuthenticationBaseUri(apiBaseUrl, ApiBaseUrlEnvironmentVariableName),
                storefrontBaseUri: NormalizeBaseUri(storefrontBaseUrl, StorefrontBaseUrlEnvironmentVariableName),
                clientAppBaseUri: NormalizeBaseUri(clientAppBaseUrl, ClientAppBaseUrlEnvironmentVariableName),
                allowInvalidCertificate: allowInvalidCertificate,
                requireConfiguration: requireConfiguration,
                refreshCookieName: string.IsNullOrWhiteSpace(refreshCookieName) ? DefaultRefreshCookieName : refreshCookieName.Trim());
        }

        public string ResolveAuthenticationUrl(string actionPath)
        {
            EnsureEnabled();
            var normalizedActionPath = NormalizeActionPath(actionPath, nameof(actionPath));
            return new Uri(AuthenticationBaseUri, normalizedActionPath).ToString();
        }

        public string ResolveClientAppUrl(string routePath)
        {
            EnsureEnabled();
            var relativeTarget = ToRelativeRequestTarget(routePath, nameof(routePath));
            return new Uri(ClientAppBaseUri, relativeTarget).ToString();
        }

        public string ResolveStorefrontUrl(string routePath)
        {
            EnsureEnabled();
            var relativeTarget = ToRelativeRequestTarget(routePath, nameof(routePath));
            return new Uri(StorefrontBaseUri, relativeTarget).ToString();
        }

        public string ToStorefrontRequestTarget(string routePath)
        {
            EnsureEnabled();
            return ToRelativeRequestTarget(routePath, nameof(routePath));
        }

        private void EnsureEnabled()
        {
            if (!IsEnabled)
            {
                throw new InvalidOperationException($"Set {ApiBaseUrlEnvironmentVariableName}, {StorefrontBaseUrlEnvironmentVariableName}, and {ClientAppBaseUrlEnvironmentVariableName} to enable the auth smoke suite.");
            }
        }

        private static bool AreAllBlank(params string?[] values)
        {
            return values.All(string.IsNullOrWhiteSpace);
        }

        private static Uri NormalizeAuthenticationBaseUri(string rawValue, string environmentVariableName)
        {
            var baseUri = NormalizeBaseUri(rawValue, environmentVariableName);
            var normalizedPath = baseUri.AbsolutePath.TrimEnd('/');

            if (normalizedPath.EndsWith("/api/authentication", StringComparison.OrdinalIgnoreCase))
            {
                return baseUri;
            }

            if (normalizedPath.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(baseUri, "authentication/");
            }

            return new Uri(baseUri, "api/authentication/");
        }

        private static Uri NormalizeBaseUri(string rawValue, string environmentVariableName)
        {
            if (!Uri.TryCreate(rawValue.Trim(), UriKind.Absolute, out var baseUri)
                || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException($"{environmentVariableName} must be an absolute http or https URL.");
            }

            var builder = new UriBuilder(baseUri);
            builder.Path = string.IsNullOrEmpty(builder.Path) || builder.Path == "/"
                ? "/"
                : $"{builder.Path.TrimEnd('/')}/";

            return builder.Uri;
        }

        private static string NormalizeActionPath(string actionPath, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(actionPath))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
            }

            var trimmedValue = actionPath.Trim();
            if (HasExplicitUriScheme(trimmedValue) || trimmedValue.StartsWith("//", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{parameterName} must be a relative auth action segment.");
            }

            return trimmedValue.Trim('/');
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
                _ => throw new InvalidOperationException($"{environmentVariableName} must be one of: true, false, 1, 0, yes, no."),
            };
        }

        private static string ToRelativeRequestTarget(string routePath, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(routePath))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", parameterName);
            }

            var normalizedPath = routePath.Trim();
            if (HasExplicitUriScheme(normalizedPath) || normalizedPath.StartsWith("//", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{parameterName} must be a relative route path.");
            }

            if (!normalizedPath.StartsWith("/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{parameterName} must begin with '/'.");
            }

            return normalizedPath == "/"
                ? string.Empty
                : normalizedPath.TrimStart('/');
        }

        private static bool HasExplicitUriScheme(string value)
        {
            var trimmedValue = value.Trim();
            var colonIndex = trimmedValue.IndexOf(':');
            if (colonIndex <= 0)
            {
                return false;
            }

            var firstPathSeparatorIndex = trimmedValue.IndexOfAny(['/', '?', '#']);
            return firstPathSeparatorIndex < 0 || colonIndex < firstPathSeparatorIndex;
        }
    }
}
