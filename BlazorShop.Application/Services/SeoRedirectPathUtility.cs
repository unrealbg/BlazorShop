namespace BlazorShop.Application.Services
{
    internal static class SeoRedirectPathUtility
    {
        public static string? NormalizePath(string? path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? null
                : path.Trim();
        }

        public static bool IsRootRelativePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            if (!path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
            {
                return false;
            }

            return !path.Any(char.IsWhiteSpace);
        }

        public static bool PathsEqual(string? left, string? right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}