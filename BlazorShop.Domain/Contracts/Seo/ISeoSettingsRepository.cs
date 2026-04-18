namespace BlazorShop.Domain.Contracts.Seo
{
    using BlazorShop.Domain.Entities;

    public interface ISeoSettingsRepository
    {
        Task<SeoSettings?> GetCurrentAsync();
    }
}