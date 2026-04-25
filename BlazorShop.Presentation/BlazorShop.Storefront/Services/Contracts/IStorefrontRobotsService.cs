namespace BlazorShop.Storefront.Services.Contracts
{
    public interface IStorefrontRobotsService
    {
        Task<string> GenerateAsync(CancellationToken cancellationToken = default);
    }
}