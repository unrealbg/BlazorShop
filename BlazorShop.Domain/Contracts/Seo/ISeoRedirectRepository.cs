namespace BlazorShop.Domain.Contracts.Seo
{
    using BlazorShop.Domain.Entities;

    public interface ISeoRedirectRepository
    {
        Task<bool> OldPathExistsAsync(string oldPath, Guid? excludedRedirectId = null);

        Task<SeoRedirect?> GetByOldPathAsync(string oldPath);

        Task<SeoRedirect?> GetActiveByOldPathAsync(string oldPath);
    }
}