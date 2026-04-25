namespace BlazorShop.Application.Diagnostics
{
    public static class SeoRuntimeEventNames
    {
        public const string PublicProductResolved = "public.product.resolved";
        public const string PublicProductNotFound = "public.product.not_found";
        public const string PublicProductServiceUnavailable = "public.product.service_unavailable";
        public const string PublicCategoryResolved = "public.category.resolved";
        public const string PublicCategoryNotFound = "public.category.not_found";
        public const string PublicCategoryServiceUnavailable = "public.category.service_unavailable";
        public const string PublicRedirectResolved = "public.redirect.resolved";
        public const string PublicRedirectLoopBlocked = "public.redirect.loop_blocked";
        public const string PublicRedirectChainBlocked = "public.redirect.chain_blocked";
        public const string PublicRedirectInvalidTargetBlocked = "public.redirect.invalid_target_blocked";
        public const string PublicDiscoverySitemapFailure = "public.discovery.sitemap_failure";
        public const string PublicDiscoveryRobotsFailure = "public.discovery.robots_failure";
    }
}