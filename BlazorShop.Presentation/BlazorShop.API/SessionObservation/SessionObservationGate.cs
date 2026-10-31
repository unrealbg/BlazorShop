namespace BlazorShop.API.SessionObservation
{
    using System.Security.Cryptography;

    using BlazorShop.Application.Options;

    using Microsoft.Extensions.Options;

    // This capability is scoped to observation metadata. It never creates a principal
    // and cannot satisfy the bearer/role policies on business endpoints.
    public sealed class SessionObservationGate(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context, IOptions<SessionObservationOptions> configured, IHostEnvironment environment)
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<SessionObservationEndpointAttribute>() is null)
            {
                await next(context);
                return;
            }

            context.Response.Headers.CacheControl = "private, no-store";
            var options = configured.Value;
            if (!options.Enabled)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var transportAllowed = context.Request.IsHttps
                || (environment.IsDevelopment() && options.AllowInsecureDevelopmentTransport);
            if (!transportAllowed || !ValidCredential(context, options))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next(context);
        }

        private static bool ValidCredential(HttpContext context, SessionObservationOptions options)
        {
            var supplied = context.Request.Headers[SessionObservationOptions.CredentialHeader];
            if (!options.HasValidKey() || supplied.Count != 1 || supplied[0] is not { Length: <= 64 } value) return false;
            Span<byte> actual = stackalloc byte[32];
            Span<byte> expected = stackalloc byte[32];
            return Convert.TryFromBase64String(value, actual, out var actualLength) && actualLength == 32
                && Convert.TryFromBase64String(options.ServiceKey, expected, out var expectedLength) && expectedLength == 32
                && CryptographicOperations.FixedTimeEquals(actual, expected);
        }
    }
}
