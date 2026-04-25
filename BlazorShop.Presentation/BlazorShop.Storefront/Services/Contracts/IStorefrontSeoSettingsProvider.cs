namespace BlazorShop.Storefront.Services.Contracts
{
    using BlazorShop.Application.DTOs.Seo;

    public interface IStorefrontSeoSettingsProvider
    {
        Task<SeoSettingsDto?> GetAsync(CancellationToken cancellationToken = default);
    }
}