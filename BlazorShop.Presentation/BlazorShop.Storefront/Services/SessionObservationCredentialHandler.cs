namespace BlazorShop.Storefront.Services
{
    using BlazorShop.Application.Options;

    using Microsoft.Extensions.Options;

    public sealed class SessionObservationCredentialHandler(IOptions<SessionObservationOptions> options, IHostEnvironment environment)
        : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Runs after service discovery, immediately before the primary transport.
            // Redirects are disabled on that transport, so the capability cannot follow one.
            var secure = request.RequestUri?.Scheme == Uri.UriSchemeHttps;
            var developmentTransport = environment.IsDevelopment() && options.Value.AllowInsecureDevelopmentTransport;
            if (!options.Value.Enabled || !options.Value.HasValidKey() || (!secure && !developmentTransport))
                throw new InvalidOperationException("Session observation transport is unavailable.");

            request.Headers.Remove(SessionObservationOptions.CredentialHeader);
            request.Headers.Add(SessionObservationOptions.CredentialHeader, options.Value.ServiceKey);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
