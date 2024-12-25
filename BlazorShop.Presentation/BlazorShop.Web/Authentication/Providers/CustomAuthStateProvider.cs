namespace BlazorShop.Web.Authentication.Providers
{
    using System.IdentityModel.Tokens.Jwt;
    using System.Security.Claims;

    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Helper.Contracts;

    using Microsoft.AspNetCore.Components.Authorization;

    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly ITokenService _tokenService;

        private ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

        public CustomAuthStateProvider(ITokenService tokenService)
        {
            _tokenService = tokenService;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                var jwt = await _tokenService.GetJwtTokenAsync(Constant.Cookie.Name);

                if (string.IsNullOrEmpty(jwt))
                {
                    return await Task.FromResult(new AuthenticationState(_anonymous));
                }

                var claims = GetClaims(jwt);

                if (!claims.Any())
                {
                    return await Task.FromResult(new AuthenticationState(_anonymous));
                }

                var claimPrincipal = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwtAuth"));
                return await Task.FromResult(new AuthenticationState(claimPrincipal));
            }
            catch
            {
                return await Task.FromResult(new AuthenticationState(_anonymous));
            }
        }

        public void NotifyAuthenticationState()
        {
            this.NotifyAuthenticationStateChanged(this.GetAuthenticationStateAsync());
        }

        private static IEnumerable<Claim> GetClaims(string jwt)
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwt);
            return token.Claims.ToList();
        }
    }
}
