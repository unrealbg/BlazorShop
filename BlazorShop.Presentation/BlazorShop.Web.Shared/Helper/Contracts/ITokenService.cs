namespace BlazorShop.Web.Shared.Helper.Contracts
{
    public interface ITokenService
    {
        Task<string> GetJwtTokenAsync(string key);

        Task StoreJwtTokenAsync(string key, string value);

        Task RemoveJwtTokenAsync(string key);
    }
}
