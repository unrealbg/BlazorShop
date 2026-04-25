namespace BlazorShop.Web.Authentication.Providers
{
    using System.Net;
    using System.Net.Http.Headers;

    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models;

    public class RefreshTokenHandler : DelegatingHandler
    {
        private static readonly HttpRequestOptionsKey<bool> RetriedKey = new("X-Refresh-Retried");

        private readonly IAuthenticationSessionRefresher _sessionRefresher;

        public RefreshTokenHandler(
            IAuthenticationSessionRefresher sessionRefresher)
        {
            _sessionRefresher = sessionRefresher;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                return response;
            }

            if (!request.Options.TryGetValue(RetriedKey, out var retried) || !retried)
            {
                var loginResponse = await _sessionRefresher.TryRefreshAsync();
                if (loginResponse is not null)
                {
                    if (request.Method == HttpMethod.Get || request.Content is null)
                    {
                        request.Options.Set(RetriedKey, true);
                        request.Headers.Authorization = new AuthenticationHeaderValue(Constant.Authentication.Type, loginResponse.Token);
                        response.Dispose();
                        return await base.SendAsync(request, cancellationToken);
                    }
                }
            }

            return response;
        }
    }
}
