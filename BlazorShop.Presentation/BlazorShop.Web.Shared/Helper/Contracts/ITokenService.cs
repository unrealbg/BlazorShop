namespace BlazorShop.Web.Shared.Helper.Contracts
{
    public interface ITokenService
    {
        Task<string> GetJwtTokenAsync(string key);

        Task<string> GetRefreshTokenAsync(string key);

        string FromToken(string jwtToken, string refreshToken);

        Task SetCookie(string key, string value, int days, string path);

        Task RemoveCookie(string key);
    }
}
