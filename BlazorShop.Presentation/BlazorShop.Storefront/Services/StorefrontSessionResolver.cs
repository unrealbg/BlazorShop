namespace BlazorShop.Storefront.Services
{
    using System.Net.Http.Json;
    using System.Security.Claims;
    using System.Text;
    using System.Text.Json;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Storefront.Services.Contracts;
    using BlazorShop.Web.Shared;

    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Configuration;

    public sealed class StorefrontSessionResolver : IStorefrontSessionResolver
    {
        private const string DefaultRefreshTokenCookieName = "__Host-blazorshop-refresh";

        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfiguration _configuration;

        public StorefrontSessionResolver(HttpClient httpClient, IHttpContextAccessor httpContextAccessor, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
            _configuration = configuration;
        }

        public async Task<StorefrontSessionInfo> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                return StorefrontSessionInfo.Anonymous;
            }

            var cookieName = GetRefreshTokenCookieName();
            if (!httpContext.Request.Cookies.TryGetValue(cookieName, out var refreshToken)
                || string.IsNullOrWhiteSpace(refreshToken))
            {
                return StorefrontSessionInfo.Anonymous;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "authentication/refresh-token");
            request.Headers.TryAddWithoutValidation("Cookie", $"{cookieName}={Uri.EscapeDataString(refreshToken)}");

            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            CopySetCookieHeaders(response, httpContext.Response);

            if (!response.IsSuccessStatusCode)
            {
                return StorefrontSessionInfo.Anonymous;
            }

            var payload = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
            if (payload is null || !payload.Success || string.IsNullOrWhiteSpace(payload.Token))
            {
                return StorefrontSessionInfo.Anonymous;
            }

            return ParseSession(payload.Token);
        }

        private string GetRefreshTokenCookieName()
        {
            return string.IsNullOrWhiteSpace(_configuration["Api:RefreshTokenCookieName"])
                ? DefaultRefreshTokenCookieName
                : _configuration["Api:RefreshTokenCookieName"]!;
        }

        private static void CopySetCookieHeaders(HttpResponseMessage response, HttpResponse storefrontResponse)
        {
            if (!response.Headers.TryGetValues("Set-Cookie", out var values))
            {
                return;
            }

            foreach (var value in values)
            {
                storefrontResponse.Headers.Append("Set-Cookie", value);
            }
        }

        private static StorefrontSessionInfo ParseSession(string token)
        {
            var tokenParts = token.Split('.');
            if (tokenParts.Length < 2)
            {
                return StorefrontSessionInfo.Anonymous;
            }

            try
            {
                using var document = JsonDocument.Parse(DecodeTokenPayload(tokenParts[1]));
                var root = document.RootElement;

                var roleClaims = ReadClaimValues(root, ClaimTypes.Role)
                    .Concat(ReadClaimValues(root, "role"));

                var isAdmin = roleClaims.Any(role => string.Equals(role, Constant.Administration.AdminRole, StringComparison.OrdinalIgnoreCase));

                var displayName = ReadClaimValue(root, "FullName")
                    ?? ReadClaimValue(root, ClaimTypes.Name)
                    ?? ReadClaimValue(root, "unique_name")
                    ?? ReadClaimValue(root, ClaimTypes.Email)
                    ?? ReadClaimValue(root, "email");

                var email = ReadClaimValue(root, ClaimTypes.Email)
                    ?? ReadClaimValue(root, "email");

                return new StorefrontSessionInfo(true, isAdmin, displayName, email);
            }
            catch
            {
                return StorefrontSessionInfo.Anonymous;
            }
        }

        private static IEnumerable<string> ReadClaimValues(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property))
            {
                return [];
            }

            return property.ValueKind switch
            {
                JsonValueKind.Array => property.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString()!)
                    .ToArray(),
                JsonValueKind.String => [property.GetString()!],
                _ => [],
            };
        }

        private static string? ReadClaimValue(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return property.GetString();
        }

        private static string DecodeTokenPayload(string encodedPayload)
        {
            var normalized = encodedPayload.Replace('-', '+').Replace('_', '/');
            var padded = (normalized.Length % 4) switch
            {
                2 => normalized + "==",
                3 => normalized + "=",
                _ => normalized,
            };

            return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        }
    }
}