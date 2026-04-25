namespace BlazorShop.Tests.Application.Diagnostics
{
    using BlazorShop.Application.Diagnostics;

    using Xunit;

    public class SeoRuntimeEventNamesTests
    {
        [Fact]
        public void SeoRuntimeEventNames_AreStableAndUnique()
        {
            var eventNames = new[]
            {
                SeoRuntimeEventNames.PublicProductResolved,
                SeoRuntimeEventNames.PublicProductNotFound,
                SeoRuntimeEventNames.PublicProductServiceUnavailable,
                SeoRuntimeEventNames.PublicCategoryResolved,
                SeoRuntimeEventNames.PublicCategoryNotFound,
                SeoRuntimeEventNames.PublicCategoryServiceUnavailable,
                SeoRuntimeEventNames.PublicRedirectResolved,
                SeoRuntimeEventNames.PublicRedirectLoopBlocked,
                SeoRuntimeEventNames.PublicRedirectChainBlocked,
                SeoRuntimeEventNames.PublicRedirectInvalidTargetBlocked,
                SeoRuntimeEventNames.PublicDiscoverySitemapFailure,
                SeoRuntimeEventNames.PublicDiscoveryRobotsFailure,
            };

            Assert.Equal("public.product.resolved", SeoRuntimeEventNames.PublicProductResolved);
            Assert.Equal("public.product.not_found", SeoRuntimeEventNames.PublicProductNotFound);
            Assert.Equal("public.product.service_unavailable", SeoRuntimeEventNames.PublicProductServiceUnavailable);
            Assert.Equal("public.category.resolved", SeoRuntimeEventNames.PublicCategoryResolved);
            Assert.Equal("public.category.not_found", SeoRuntimeEventNames.PublicCategoryNotFound);
            Assert.Equal("public.category.service_unavailable", SeoRuntimeEventNames.PublicCategoryServiceUnavailable);
            Assert.Equal("public.redirect.resolved", SeoRuntimeEventNames.PublicRedirectResolved);
            Assert.Equal("public.redirect.loop_blocked", SeoRuntimeEventNames.PublicRedirectLoopBlocked);
            Assert.Equal("public.redirect.chain_blocked", SeoRuntimeEventNames.PublicRedirectChainBlocked);
            Assert.Equal("public.redirect.invalid_target_blocked", SeoRuntimeEventNames.PublicRedirectInvalidTargetBlocked);
            Assert.Equal("public.discovery.sitemap_failure", SeoRuntimeEventNames.PublicDiscoverySitemapFailure);
            Assert.Equal("public.discovery.robots_failure", SeoRuntimeEventNames.PublicDiscoveryRobotsFailure);
            Assert.Equal(eventNames.Length, eventNames.Distinct(StringComparer.Ordinal).Count());
        }
    }
}