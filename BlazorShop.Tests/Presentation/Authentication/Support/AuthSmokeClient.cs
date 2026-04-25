namespace BlazorShop.Tests.Presentation.Authentication
{
    using System.Net;
    using System.Net.Http.Json;

    using BlazorShop.Application.DTOs.UserIdentity;

    internal sealed class AuthSmokeClient : IDisposable
    {
        private static readonly DecompressionMethods SupportedDecompressionMethods = DecompressionMethods.Brotli | DecompressionMethods.Deflate | DecompressionMethods.GZip;

        private readonly HttpClient _authenticationClient;
        private readonly CookieContainer _cookieContainer;
        private readonly HttpClient _storefrontClient;

        public AuthSmokeClient(AuthSmokeSettings settings)
        {
            Settings = settings;
            _cookieContainer = new CookieContainer();
            _authenticationClient = CreateClient(settings.AuthenticationBaseUri, settings.AllowInvalidCertificate, _cookieContainer);
            _storefrontClient = CreateClient(settings.StorefrontBaseUri, settings.AllowInvalidCertificate, _cookieContainer);
        }

        public AuthSmokeSettings Settings { get; }

        public Task<HttpResponseMessage> CreateUserAsync(CreateUser user, CancellationToken cancellationToken = default)
        {
            return _authenticationClient.PostAsJsonAsync("create", user, cancellationToken);
        }

        public string? GetRefreshTokenCookieValue()
        {
            var cookies = _cookieContainer.GetCookies(Settings.AuthenticationBaseUri);
            foreach (Cookie cookie in cookies)
            {
                if (string.Equals(cookie.Name, Settings.RefreshCookieName, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(cookie.Value))
                {
                    return cookie.Value;
                }
            }

            return null;
        }

        public Task<HttpResponseMessage> GetCheckoutAsync(CancellationToken cancellationToken = default)
        {
            return _storefrontClient.GetAsync(Settings.ToStorefrontRequestTarget("/checkout"), cancellationToken);
        }

        public Task<HttpResponseMessage> LoginAsync(LoginUser user, CancellationToken cancellationToken = default)
        {
            return _authenticationClient.PostAsJsonAsync("login", user, cancellationToken);
        }

        public Task<HttpResponseMessage> LogoutAsync(CancellationToken cancellationToken = default)
        {
            return _authenticationClient.PostAsync("logout", content: null, cancellationToken);
        }

        public Task<HttpResponseMessage> RefreshAsync(CancellationToken cancellationToken = default)
        {
            return _authenticationClient.PostAsync("refresh-token", content: null, cancellationToken);
        }

        public void Dispose()
        {
            _authenticationClient.Dispose();
            _storefrontClient.Dispose();
        }

        private static HttpClient CreateClient(Uri baseUri, bool allowInvalidCertificate, CookieContainer cookieContainer)
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = SupportedDecompressionMethods,
                CookieContainer = cookieContainer,
                UseCookies = true,
            };

            if (allowInvalidCertificate)
            {
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            var client = new HttpClient(handler)
            {
                BaseAddress = baseUri,
                Timeout = TimeSpan.FromSeconds(20),
            };

            client.DefaultRequestHeaders.UserAgent.ParseAdd("BlazorShop-Auth-Smoke/1.0");
            return client;
        }
    }
}