namespace BlazorShop.Application.Validations.Seo
{
    internal static class SeoValidationRules
    {
        public static bool BeAbsoluteUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        public static bool BeAbsoluteUrlOrRootRelativePath(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                || BeAbsoluteUrl(value)
                || BeRootRelativePath(value);
        }

        public static bool BeRootRelativePath(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!value.StartsWith("/", StringComparison.Ordinal) || value.StartsWith("//", StringComparison.Ordinal))
            {
                return false;
            }

            return !value.Any(char.IsWhiteSpace);
        }
    }
}