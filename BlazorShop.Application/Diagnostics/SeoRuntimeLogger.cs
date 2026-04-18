namespace BlazorShop.Application.Diagnostics
{
    using Microsoft.Extensions.Logging;

    public static partial class SeoRuntimeLogger
    {
        public static void PublicProductResolved(ILogger logger, string routePath, string slug, Guid productId)
        {
            PublicProductResolvedCore(logger, SeoRuntimeEventNames.PublicProductResolved, routePath, slug, productId);
        }

        public static void PublicProductNotFound(ILogger logger, string routePath, string slug)
        {
            PublicProductNotFoundCore(logger, SeoRuntimeEventNames.PublicProductNotFound, routePath, slug);
        }

        public static void PublicProductServiceUnavailable(ILogger logger, string routePath, string slug)
        {
            PublicProductServiceUnavailableCore(logger, SeoRuntimeEventNames.PublicProductServiceUnavailable, routePath, slug);
        }

        public static void PublicCategoryResolved(ILogger logger, string routePath, string slug, Guid categoryId)
        {
            PublicCategoryResolvedCore(logger, SeoRuntimeEventNames.PublicCategoryResolved, routePath, slug, categoryId);
        }

        public static void PublicCategoryNotFound(ILogger logger, string routePath, string slug)
        {
            PublicCategoryNotFoundCore(logger, SeoRuntimeEventNames.PublicCategoryNotFound, routePath, slug);
        }

        public static void PublicCategoryServiceUnavailable(ILogger logger, string routePath, string slug)
        {
            PublicCategoryServiceUnavailableCore(logger, SeoRuntimeEventNames.PublicCategoryServiceUnavailable, routePath, slug);
        }

        public static void PublicRedirectResolved(ILogger logger, string sourcePath, string destinationPath, int statusCode)
        {
            PublicRedirectResolvedCore(logger, SeoRuntimeEventNames.PublicRedirectResolved, sourcePath, destinationPath, statusCode);
        }

        public static void PublicRedirectLoopBlocked(ILogger logger, string sourcePath, string currentPath, int hopCount)
        {
            PublicRedirectLoopBlockedCore(logger, SeoRuntimeEventNames.PublicRedirectLoopBlocked, sourcePath, currentPath, hopCount);
        }

        public static void PublicRedirectChainBlocked(ILogger logger, string sourcePath, int maxHops)
        {
            PublicRedirectChainBlockedCore(logger, SeoRuntimeEventNames.PublicRedirectChainBlocked, sourcePath, maxHops);
        }

        public static void PublicRedirectInvalidTargetBlocked(ILogger logger, string sourcePath, string targetPath, int statusCode)
        {
            PublicRedirectInvalidTargetBlockedCore(logger, SeoRuntimeEventNames.PublicRedirectInvalidTargetBlocked, sourcePath, targetPath, statusCode);
        }

        public static void PublicDiscoverySitemapFailure(ILogger logger, string documentPath, string failureReason)
        {
            PublicDiscoverySitemapFailureCore(logger, SeoRuntimeEventNames.PublicDiscoverySitemapFailure, documentPath, failureReason);
        }

        public static void PublicDiscoverySitemapFailure(ILogger logger, Exception exception, string documentPath, string failureReason)
        {
            PublicDiscoverySitemapFailureErrorCore(logger, exception, SeoRuntimeEventNames.PublicDiscoverySitemapFailure, documentPath, failureReason);
        }

        public static void PublicDiscoveryRobotsFailure(ILogger logger, string documentPath, string failureReason)
        {
            PublicDiscoveryRobotsFailureCore(logger, SeoRuntimeEventNames.PublicDiscoveryRobotsFailure, documentPath, failureReason);
        }

        public static void PublicDiscoveryRobotsFailure(ILogger logger, Exception exception, string documentPath, string failureReason)
        {
            PublicDiscoveryRobotsFailureErrorCore(logger, exception, SeoRuntimeEventNames.PublicDiscoveryRobotsFailure, documentPath, failureReason);
        }

        [LoggerMessage(EventId = 7001, Level = LogLevel.Information, Message = "{SeoEvent} resolved public product route {RoutePath} for slug {Slug} and product id {ProductId}.")]
        private static partial void PublicProductResolvedCore(ILogger logger, string seoEvent, string routePath, string slug, Guid productId);

        [LoggerMessage(EventId = 7002, Level = LogLevel.Warning, Message = "{SeoEvent} did not find a published product for route {RoutePath} and slug {Slug}.")]
        private static partial void PublicProductNotFoundCore(ILogger logger, string seoEvent, string routePath, string slug);

        [LoggerMessage(EventId = 7003, Level = LogLevel.Warning, Message = "{SeoEvent} could not resolve public product route {RoutePath} for slug {Slug} because the catalog service is unavailable.")]
        private static partial void PublicProductServiceUnavailableCore(ILogger logger, string seoEvent, string routePath, string slug);

        [LoggerMessage(EventId = 7004, Level = LogLevel.Information, Message = "{SeoEvent} resolved public category route {RoutePath} for slug {Slug} and category id {CategoryId}.")]
        private static partial void PublicCategoryResolvedCore(ILogger logger, string seoEvent, string routePath, string slug, Guid categoryId);

        [LoggerMessage(EventId = 7005, Level = LogLevel.Warning, Message = "{SeoEvent} did not find a published category for route {RoutePath} and slug {Slug}.")]
        private static partial void PublicCategoryNotFoundCore(ILogger logger, string seoEvent, string routePath, string slug);

        [LoggerMessage(EventId = 7006, Level = LogLevel.Warning, Message = "{SeoEvent} could not resolve public category route {RoutePath} for slug {Slug} because the catalog service is unavailable.")]
        private static partial void PublicCategoryServiceUnavailableCore(ILogger logger, string seoEvent, string routePath, string slug);

        [LoggerMessage(EventId = 7007, Level = LogLevel.Information, Message = "{SeoEvent} resolved public redirect from {SourcePath} to {DestinationPath} with status code {StatusCode}.")]
        private static partial void PublicRedirectResolvedCore(ILogger logger, string seoEvent, string sourcePath, string destinationPath, int statusCode);

        [LoggerMessage(EventId = 7008, Level = LogLevel.Warning, Message = "{SeoEvent} blocked redirect resolution for source path {SourcePath} after encountering a loop at {CurrentPath} on hop {HopCount}.")]
        private static partial void PublicRedirectLoopBlockedCore(ILogger logger, string seoEvent, string sourcePath, string currentPath, int hopCount);

        [LoggerMessage(EventId = 7009, Level = LogLevel.Warning, Message = "{SeoEvent} blocked redirect resolution for source path {SourcePath} after exceeding {MaxHops} hops.")]
        private static partial void PublicRedirectChainBlockedCore(ILogger logger, string seoEvent, string sourcePath, int maxHops);

        [LoggerMessage(EventId = 7010, Level = LogLevel.Warning, Message = "{SeoEvent} blocked redirect resolution for source path {SourcePath} because target {TargetPath} with status code {StatusCode} is invalid.")]
        private static partial void PublicRedirectInvalidTargetBlockedCore(ILogger logger, string seoEvent, string sourcePath, string targetPath, int statusCode);

        [LoggerMessage(EventId = 7011, Level = LogLevel.Warning, Message = "{SeoEvent} failed to serve discovery document {DocumentPath} because {FailureReason}.")]
        private static partial void PublicDiscoverySitemapFailureCore(ILogger logger, string seoEvent, string documentPath, string failureReason);

        [LoggerMessage(EventId = 7012, Level = LogLevel.Error, Message = "{SeoEvent} failed to serve discovery document {DocumentPath} because {FailureReason}.")]
        private static partial void PublicDiscoverySitemapFailureErrorCore(ILogger logger, Exception exception, string seoEvent, string documentPath, string failureReason);

        [LoggerMessage(EventId = 7013, Level = LogLevel.Warning, Message = "{SeoEvent} failed to serve discovery document {DocumentPath} because {FailureReason}.")]
        private static partial void PublicDiscoveryRobotsFailureCore(ILogger logger, string seoEvent, string documentPath, string failureReason);

        [LoggerMessage(EventId = 7014, Level = LogLevel.Error, Message = "{SeoEvent} failed to serve discovery document {DocumentPath} because {FailureReason}.")]
        private static partial void PublicDiscoveryRobotsFailureErrorCore(ILogger logger, Exception exception, string seoEvent, string documentPath, string failureReason);
    }
}