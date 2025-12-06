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
                var token = await _cookieStorage.GetAsync(key);
                if (string.IsNullOrWhiteSpace(token))
                {
                    return string.Empty;
                }

                var parts = token.Split("--", StringSplitOptions.RemoveEmptyEntries);
                return parts.Length > position ? parts[position] : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
