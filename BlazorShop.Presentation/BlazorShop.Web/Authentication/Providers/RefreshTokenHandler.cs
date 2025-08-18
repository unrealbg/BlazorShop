namespace BlazorShop.Web.Authentication.Providers
{
    using System.Net;

    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class RefreshTokenHandler : DelegatingHandler
    {
        private static readonly HttpRequestOptionsKey<bool> RetriedKey = new("X-Refresh-Retried");

        private readonly ITokenService _tokenService;
        private readonly IHttpClientHelper _httpClientHelper;
        private readonly IAuthenticationService _authenticationService;

        public RefreshTokenHandler(ITokenService tokenService, IHttpClientHelper httpClientHelper, IAuthenticationService authenticationService)
        {
            this._tokenService = tokenService;
            this._httpClientHelper = httpClientHelper;
            this._authenticationService = authenticationService;
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
                var refreshToken = await _tokenService.GetJwtTokenAsync(Constant.Cookie.Name);
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    var loginResponse = await this.MakeApiCall(refreshToken);
                    if (loginResponse is not null)
                    {
                        await this._httpClientHelper.GetPrivateClientAsync();

                        if (request.Method == HttpMethod.Get || request.Content is null)
                        {
                            request.Options.Set(RetriedKey, true);
                            response.Dispose();
                            return await base.SendAsync(request, cancellationToken);
                        }
                    }
                }
            }

            return response;
        }

        private async Task<LoginResponse?> MakeApiCall(string refreshToken)
        {
            var result = await _authenticationService.ReviveToken(refreshToken);

            if (result.Success)
            {
                var cookieValue = _tokenService.FromToken(result.Token, result.RefreshToken);
                await _tokenService.RemoveCookie(Constant.Cookie.Name);
                await _tokenService.SetCookie(
                    Constant.Cookie.Name,
                    cookieValue,
                    Constant.Cookie.Days,
                    Constant.Cookie.Path);

                return result;
            }

            return null;
        }
    }
}
