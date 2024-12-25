namespace BlazorShop.Web.Authentication.Providers
{
    using System.Net;

    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class RefreshTokenHandler : DelegatingHandler
    {
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
            var isPost = request.Method == HttpMethod.Post;
            var isPut = request.Method == HttpMethod.Put;
            var isDelete = request.Method == HttpMethod.Delete;

            var result = await base.SendAsync(request, cancellationToken);

            if (isPost || isPut || isDelete)
            {
                if (result.StatusCode != HttpStatusCode.Unauthorized)
                {
                    return result;
                }

                var refreshToken = await _tokenService.GetJwtTokenAsync(Constant.Cookie.Name);

                if (string.IsNullOrEmpty(refreshToken))
                {
                    return result;
                }

                var loginResponse = await this.MakeApiCall(refreshToken);

                if (loginResponse == null)
                {
                    return result;
                }

                await this._httpClientHelper.GetPrivateClientAsync();

                return await base.SendAsync(request, cancellationToken);
            }

            return result;
        }

        private async Task<LoginResponse> MakeApiCall(string refreshToken)
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
