namespace BlazorShop.Domain.Contracts.Seo
{
    public interface ISeoRedirectRepository
    {
        Task<bool> OldPathExistsAsync(string oldPath, Guid? excludedRedirectId = null);
    }
}