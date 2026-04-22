namespace BlazorShop.Storefront.Services.Contracts
{
    public interface IStorefrontClientAppUrlResolver
    {
        string? ResolveBaseUrl();

        string ResolveUrl(string? relativeOrAbsoluteUrl);
    }
}