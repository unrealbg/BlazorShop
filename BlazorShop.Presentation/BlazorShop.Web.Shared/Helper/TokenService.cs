namespace BlazorShop.Web.Shared.Helper
{
    using BlazorShop.Web.Shared.BrowserStorage.Contracts;
    using BlazorShop.Web.Shared.Helper.Contracts;

    public class TokenService : ITokenService
    {
        private readonly IBrowserSessionStorageService _sessionStorage;

        public TokenService(IBrowserSessionStorageService sessionStorage)
        {
            _sessionStorage = sessionStorage;
        }

        public async Task<string> GetJwtTokenAsync(string key)
        {
            try
            {
                return await _sessionStorage.GetAsync(key) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        public async Task StoreJwtTokenAsync(string key, string value)
        {
            await _sessionStorage.SetAsync(key, value);
        }

        public async Task RemoveJwtTokenAsync(string key)
        {
            await _sessionStorage.RemoveAsync(key);
        }
    }
}
