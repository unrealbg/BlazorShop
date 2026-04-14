namespace BlazorShop.Web.Authentication.Providers
{
    using Microsoft.AspNetCore.Components.WebAssembly.Http;

    public class BrowserCredentialsHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            return base.SendAsync(request, cancellationToken);
        }
    }
}