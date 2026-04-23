namespace BlazorShop.Web.Authentication
{
    using BlazorShop.Web.Shared;

    public static class ProtectedRouteRedirectResolver
    {
        public static string? ResolveLoginRedirectPath(string? relativePath, bool isAuthenticated)
        {
            if (isAuthenticated)
            {
                return null;
            }

            var sanitizedPath = Sanitize(relativePath);

            return string.IsNullOrWhiteSpace(sanitizedPath)
                ? "/authentication/login"
                : $"/authentication/login/{sanitizedPath}";
        }

        public static string ResolvePostLoginPath(string? relativePath, bool isAdmin)
        {
            var sanitizedPath = Sanitize(relativePath);

            if (string.IsNullOrWhiteSpace(sanitizedPath)
                || string.Equals(sanitizedPath, Constant.Cart.Name, StringComparison.OrdinalIgnoreCase))
            {
                return isAdmin ? "/admin" : "/account";
            }

            var normalizedPath = sanitizedPath.StartsWith("/", StringComparison.Ordinal)
                ? sanitizedPath
                : $"/{sanitizedPath}";

            if (!isAdmin && normalizedPath.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))
            {
                return "/account";
            }

            return normalizedPath;
        }

        private static string Sanitize(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return string.Empty;
            }

            var trimmed = relativePath.Trim().TrimStart('/');
            var queryIndex = trimmed.IndexOfAny(['?', '#']);

            return queryIndex >= 0
                ? trimmed[..queryIndex]
                : trimmed;
        }
    }
}