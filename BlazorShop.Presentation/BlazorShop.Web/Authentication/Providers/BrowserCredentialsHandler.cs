namespace BlazorShop.Web.Authentication.Providers
{
    using Microsoft.AspNetCore.Components.WebAssembly.Http;

    public class BrowserCredentialsHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath
                .EndsWith("/authentication/refresh-token", StringComparison.OrdinalIgnoreCase) == true)
            {
                // A redirect must not implicitly repost a possibly consumed credential.
                request.SetBrowserRequestOption("redirect", "error");
            }
            return base.SendAsync(request, cancellationToken);
        }
    }
}
