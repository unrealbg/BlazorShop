namespace BlazorShop.Application.Services.Contracts
{
    using BlazorShop.Application.DTOs.Seo;

    public interface ISeoMetadataBuilder
    {
        SeoMetadataDto Build(SeoMetadataBuildRequest request);
    }
}