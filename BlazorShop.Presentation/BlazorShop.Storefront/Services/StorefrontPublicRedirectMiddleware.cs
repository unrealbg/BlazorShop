namespace BlazorShop.Storefront.Services
{
    using System.IO;

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

        private readonly RequestDelegate _next;

        public StorefrontPublicRedirectMiddleware(RequestDelegate next)
        {
            _next = next;
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
                context.Response.StatusCode = redirectResult.Value.StatusCode;
                context.Response.Headers.Location = BuildLocation(redirectResult.Value.NewPath!, context.Request.QueryString);
                return;
            }

            await _next(context);
        }

        private static string BuildLocation(string newPath, QueryString queryString)
        {
            if (!queryString.HasValue)
            {
                return newPath;
            }

            return newPath.Contains('?', StringComparison.Ordinal)
                ? $"{newPath}&{queryString.Value!.TrimStart('?')}"
                : $"{newPath}{queryString.Value}";
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
    }
}