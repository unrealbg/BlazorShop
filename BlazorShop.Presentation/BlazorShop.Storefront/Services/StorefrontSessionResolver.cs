namespace BlazorShop.Storefront.Services
{
    using System.Net.Http.Json;
    using System.Text.Json;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.Options;
    using BlazorShop.Storefront.Services.Contracts;

    using Microsoft.Extensions.Options;

    public sealed class StorefrontSessionResolver(
        HttpClient client,
        IHttpContextAccessor contexts,
        IConfiguration configuration,
        IOptions<SessionObservationOptions> options) : IStorefrontSessionResolver
    {
        public async Task<StorefrontSessionInfo> GetCurrentUserAsync(CancellationToken cancellationToken = default)
        {
            var context = contexts.HttpContext;
            var cookieName = configuration["Api:RefreshTokenCookieName"] ?? "__Host-blazorshop-refresh";
            if (!options.Value.Enabled || context is null
                || !context.Request.Cookies.TryGetValue(cookieName, out var credential)
                || string.IsNullOrWhiteSpace(credential)) return StorefrontSessionInfo.Anonymous;

            using var request = new HttpRequestMessage(HttpMethod.Get, "internal/storefront/session");
            request.Headers.TryAddWithoutValidation("Cookie", $"{cookieName}={Uri.EscapeDataString(credential)}");
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
                if (!response.IsSuccessStatusCode) return StorefrontSessionInfo.Anonymous;
                var state = await response.Content.ReadFromJsonAsync<SessionObservation>(deadline.Token);
                return state is { IsAuthenticated: true }
                    ? new StorefrontSessionInfo(true, state.IsAdmin, state.DisplayName, state.Email)
                    : StorefrontSessionInfo.Anonymous;
            }
            catch (HttpRequestException) { return StorefrontSessionInfo.Anonymous; }
            catch (JsonException) { return StorefrontSessionInfo.Anonymous; }
            catch (InvalidOperationException) { return StorefrontSessionInfo.Anonymous; }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return StorefrontSessionInfo.Anonymous; }
        }
    }
}
