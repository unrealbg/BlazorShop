namespace BlazorShop.Web.Services.Contracts
{
    public interface IStorefrontSeoService
    {
        Task<StorefrontSeoMetadata> BuildAsync(StorefrontSeoMetadataBuildRequest request);
    }
}