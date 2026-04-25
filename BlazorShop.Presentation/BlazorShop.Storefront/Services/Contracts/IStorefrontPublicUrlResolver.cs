namespace BlazorShop.Storefront.Services.Contracts
{
    public interface IStorefrontPublicUrlResolver
    {
        string? ResolveBaseUrl(string? configuredBaseUrl = null);

        string? ResolveAbsoluteUrl(string? relativeOrAbsoluteUrl, string? configuredBaseUrl = null);
    }
}