namespace BlazorShop.Tests.Presentation.Authentication
{
    using Xunit;

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    internal sealed class AuthSmokeFactAttribute : FactAttribute
    {
        public AuthSmokeFactAttribute()
        {
            var apiBaseUrl = Environment.GetEnvironmentVariable(AuthSmokeSettings.ApiBaseUrlEnvironmentVariableName);
            var storefrontBaseUrl = Environment.GetEnvironmentVariable(AuthSmokeSettings.StorefrontBaseUrlEnvironmentVariableName);
            var clientAppBaseUrl = Environment.GetEnvironmentVariable(AuthSmokeSettings.ClientAppBaseUrlEnvironmentVariableName);
            var requireConfiguration = Environment.GetEnvironmentVariable(AuthSmokeSettings.RequireConfigurationEnvironmentVariableName);

            if (AreAllBlank(apiBaseUrl, storefrontBaseUrl, clientAppBaseUrl) && !IsTrue(requireConfiguration))
            {
                Skip = $"Set {AuthSmokeSettings.ApiBaseUrlEnvironmentVariableName}, {AuthSmokeSettings.StorefrontBaseUrlEnvironmentVariableName}, and {AuthSmokeSettings.ClientAppBaseUrlEnvironmentVariableName} to run the live auth smoke suite.";
            }
        }

        private static bool AreAllBlank(params string?[] values)
        {
            return values.All(string.IsNullOrWhiteSpace);
        }

        private static bool IsTrue(string? rawValue)
        {
            return rawValue?.Trim().ToLowerInvariant() is "1" or "true" or "yes";
        }
    }
}