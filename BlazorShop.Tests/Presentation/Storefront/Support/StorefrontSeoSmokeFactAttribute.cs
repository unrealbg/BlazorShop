namespace BlazorShop.Tests.Presentation.Storefront
{
    using Xunit;

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class StorefrontSeoSmokeFactAttribute : FactAttribute
    {
        public StorefrontSeoSmokeFactAttribute(bool requireRedirect = false)
        {
            var baseUrl = Environment.GetEnvironmentVariable(StorefrontSeoSmokeSettings.BaseUrlEnvironmentVariableName);
            var requireConfiguration = Environment.GetEnvironmentVariable(StorefrontSeoSmokeSettings.RequireConfigurationEnvironmentVariableName);

            if (string.IsNullOrWhiteSpace(baseUrl) && !IsTrue(requireConfiguration))
            {
                Skip = $"Set {StorefrontSeoSmokeSettings.BaseUrlEnvironmentVariableName} to run the storefront SEO smoke suite.";
                return;
            }

            if (requireRedirect && IsRedirectSmokeExplicitlyDisabled())
            {
                Skip = $"Set both {StorefrontSeoSmokeSettings.RedirectSourcePathEnvironmentVariableName} and {StorefrontSeoSmokeSettings.RedirectTargetPathEnvironmentVariableName}, or leave them unset to use the default redirect smoke route.";
            }
        }

        private static bool IsRedirectSmokeExplicitlyDisabled()
        {
            var sourcePath = Environment.GetEnvironmentVariable(StorefrontSeoSmokeSettings.RedirectSourcePathEnvironmentVariableName);
            var targetPath = Environment.GetEnvironmentVariable(StorefrontSeoSmokeSettings.RedirectTargetPathEnvironmentVariableName);

            return sourcePath is not null
                && targetPath is not null
                && string.IsNullOrWhiteSpace(sourcePath)
                && string.IsNullOrWhiteSpace(targetPath);
        }

        private static bool IsTrue(string? rawValue)
        {
            return rawValue?.Trim().ToLowerInvariant() is "1" or "true" or "yes";
        }
    }
}