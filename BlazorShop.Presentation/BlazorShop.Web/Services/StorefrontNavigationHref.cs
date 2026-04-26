namespace BlazorShop.Web.Services
{
    using BlazorShop.Web.Services.Contracts;

    public static class StorefrontNavigationHref
    {
        public const string LocalStorefrontFallback = "/";

        public static string ResolveShopHref(IPublicStorefrontUrlResolver resolver)
        {
            var storefrontUrl = resolver.Resolve();

            return string.IsNullOrWhiteSpace(storefrontUrl)
                ? LocalStorefrontFallback
                : storefrontUrl;
        }
    }
}
