namespace BlazorShop.Web.Shared.BrowserStorage.Contracts
{
    public interface IBrowserSessionStorageService
    {
        Task SetAsync(string key, string value);

        Task<string?> GetAsync(string key);

        Task RemoveAsync(string key);
    }
}