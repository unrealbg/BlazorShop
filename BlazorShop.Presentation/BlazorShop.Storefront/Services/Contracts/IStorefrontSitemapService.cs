namespace BlazorShop.Storefront.Services.Contracts
{
    public interface IStorefrontSitemapService
    {
        Task<StorefrontSitemapGenerationResult> GenerateAsync(CancellationToken cancellationToken = default);
    }
}