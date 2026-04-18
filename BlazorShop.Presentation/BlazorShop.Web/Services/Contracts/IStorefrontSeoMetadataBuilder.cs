namespace BlazorShop.Web.Services.Contracts
{
    public interface IStorefrontSeoMetadataBuilder
    {
        StorefrontSeoMetadata Build(StorefrontSeoMetadataBuildRequest request);
    }
}