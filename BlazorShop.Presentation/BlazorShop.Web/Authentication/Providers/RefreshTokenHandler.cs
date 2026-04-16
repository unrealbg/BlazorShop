namespace BlazorShop.Web.Authentication.Providers
{
    using System.Net;
    using System.Net.Http.Headers;

    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class RefreshTokenHandler : DelegatingHandler
    {
        private static readonly HttpRequestOptionsKey<bool> RetriedKey = new("X-Refresh-Retried");

        private readonly ITokenService _tokenService;
        private readonly IAuthenticationService _authenticationService;
        private readonly IAuthenticationStateNotifier _authenticationStateNotifier;

        public RefreshTokenHandler(
            ITokenService tokenService,
            IAuthenticationService authenticationService,
            IAuthenticationStateNotifier authenticationStateNotifier)
        {
            this._tokenService = tokenService;
            this._authenticationService = authenticationService;
            this._authenticationStateNotifier = authenticationStateNotifier;
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
                var loginResponse = await this.MakeApiCall();
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

        private async Task<LoginResponse?> MakeApiCall()
        {
            var result = await _authenticationService.ReviveToken();

            if (result.Success && !string.IsNullOrWhiteSpace(result.Token))
            {
                await _tokenService.StoreJwtTokenAsync(Constant.TokenStorage.Key, result.Token);
                _authenticationStateNotifier.NotifyAuthenticationState();

                return result;
            }

            await _tokenService.RemoveJwtTokenAsync(Constant.TokenStorage.Key);
            _authenticationStateNotifier.NotifyAuthenticationState();
            return null;
        }
    }
}
