namespace BlazorShop.Web.Authentication.Providers
{
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Helper.Contracts;

    public sealed class AuthenticationSessionBootstrapper : IAuthenticationSessionBootstrapper
    {
        private readonly ITokenService _tokenService;
        private readonly IAuthenticationSessionRefresher _sessionRefresher;

        public AuthenticationSessionBootstrapper(ITokenService tokenService, IAuthenticationSessionRefresher sessionRefresher)
        {
            _tokenService = tokenService;
            _sessionRefresher = sessionRefresher;
        }

        public async Task RestoreAsync()
        {
            var token = await _tokenService.GetJwtTokenAsync(Constant.TokenStorage.Key);
            if (!string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            await _sessionRefresher.TryRefreshAsync(clearTokenOnFailure: false);
        }
    }
}