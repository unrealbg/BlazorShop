namespace BlazorShop.Web.Shared.Helper
{
    using BlazorShop.Web.Shared.CookieStorage.Contracts;
    using BlazorShop.Web.Shared.Helper.Contracts;

    public class TokenService : ITokenService
    {
        private readonly IBrowserCookieStorageService _cookieStorage;

        public TokenService(IBrowserCookieStorageService cookieStorage)
        {
            _cookieStorage = cookieStorage;
        }

        public async Task<string> GetJwtTokenAsync(string key)
        {
            return await GetTokenAsync(key, 0);
        }

        public async Task<string> GetRefreshTokenAsync(string key)
        {
            return await GetTokenAsync(key, 1);
        }

        public string FromToken(string jwtToken, string refreshToken)
        {
           return $"{jwtToken}--{refreshToken}";
        }

        public async Task SetCookie(string key, string value, int days, string path)
        {
            await _cookieStorage.SetAsync(key, value, days, path);
        }

        public async Task RemoveCookie(string key)
        {
            await _cookieStorage.RemoveAsync(key);
        }

        private async Task<string> GetTokenAsync(string key, int position)
        {
            try
            {
                string token = await _cookieStorage.GetAsync(key);
                return token != null ? token.Split("--")[position] : null!;
            }
            catch
            {
                return null!;
            }
        }
    }
}
