namespace BlazorShop.Storefront.Services
{
    using System.IO;

    using BlazorShop.Application.Diagnostics;

    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.Extensions.Logging;

    public sealed class StorefrontPublicRedirectMiddleware
    {
        private static readonly string[] ExcludedPrefixes =
        [
            "/api",
            "/_",
            "/css",
            "/js",
            "/images",
            "/uploads",
            "/favicon",
            "/icon-",
        ];

        private static readonly HashSet<int> RedirectStatusCodes = [301, 302, 307, 308];

        private readonly ILogger<StorefrontPublicRedirectMiddleware> _logger;
        private readonly RequestDelegate _next;

        public StorefrontPublicRedirectMiddleware(RequestDelegate next, ILogger<StorefrontPublicRedirectMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, StorefrontApiClient apiClient)
        {
            if (!ShouldResolveRedirect(context.Request))
            {
                await _next(context);
                return;
            }

            var requestPath = context.Request.Path.Value;
            if (string.IsNullOrWhiteSpace(requestPath))
            {
                await _next(context);
                return;
            }

            var redirectResult = await apiClient.GetRedirectResolutionAsync(requestPath, context.RequestAborted);
            if (redirectResult.IsSuccess && redirectResult.Value is not null && !string.IsNullOrWhiteSpace(redirectResult.Value.NewPath))
            {
                if (!TryValidateResolvedRedirect(requestPath, redirectResult.Value, out var destinationPath, out var blockReason))
                {
                    if (blockReason == RedirectBlockReason.Loop)
                    {
                        SeoRuntimeLogger.PublicRedirectLoopBlocked(_logger, requestPath, redirectResult.Value.NewPath ?? requestPath, hopCount: 1);
                    }
                    else
                    {
                        SeoRuntimeLogger.PublicRedirectInvalidTargetBlocked(_logger, requestPath, redirectResult.Value.NewPath ?? string.Empty, redirectResult.Value.StatusCode);
                    }

                    await _next(context);
                    return;
                }

                SeoRuntimeLogger.PublicRedirectResolved(_logger, requestPath, destinationPath!, redirectResult.Value.StatusCode);
                context.Response.StatusCode = redirectResult.Value.StatusCode;
                context.Response.Headers.Location = BuildLocation(destinationPath!);
                return;
            }

            await _next(context);
        }

        private static string BuildLocation(string newPath)
        {
            return newPath;
        }

        private static bool TryValidateResolvedRedirect(string sourcePath, Application.DTOs.Seo.SeoRedirectResolutionDto redirect, out string? destinationPath, out RedirectBlockReason blockReason)
        {
            destinationPath = redirect.NewPath?.Trim();
            blockReason = RedirectBlockReason.None;

            if (string.IsNullOrWhiteSpace(destinationPath)
                || !RedirectStatusCodes.Contains(redirect.StatusCode)
                || !destinationPath.StartsWith("/", StringComparison.Ordinal)
                || destinationPath.StartsWith("//", StringComparison.Ordinal)
                || destinationPath.Contains("\r", StringComparison.Ordinal)
                || destinationPath.Contains("\n", StringComparison.Ordinal))
            {
                blockReason = RedirectBlockReason.InvalidTarget;
                return false;
            }

            if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                blockReason = RedirectBlockReason.Loop;
                return false;
            }

            return true;
        }

        private static bool ShouldResolveRedirect(HttpRequest request)
        {
            if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            {
                return false;
            }

            var path = request.Path.Value;
            if (string.IsNullOrWhiteSpace(path)
                || string.Equals(path, "/", StringComparison.Ordinal)
                || string.Equals(path, StorefrontRoutes.Robots, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, StorefrontRoutes.Sitemap, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (ExcludedPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            if (Path.HasExtension(path) && !string.Equals(path, "/robots.txt", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private enum RedirectBlockReason
        {
            None,
            InvalidTarget,
            Loop,
        }
    }
}