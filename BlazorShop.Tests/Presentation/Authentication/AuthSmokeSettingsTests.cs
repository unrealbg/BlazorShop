namespace BlazorShop.Tests.Presentation.Authentication
{
    using Xunit;

    public class AuthSmokeSettingsTests
    {
        [Fact]
        public void FromLookup_WhenAllBaseUrlsAreMissing_DisablesSmokeSuite()
        {
            var settings = AuthSmokeSettings.FromLookup(_ => null);

            Assert.False(settings.IsEnabled);
            Assert.Equal("__Host-blazorshop-refresh", settings.RefreshCookieName);
        }

        [Fact]
        public void FromLookup_WhenConfigured_NormalizesTargetsAndRoutes()
        {
            var settings = AuthSmokeSettings.FromLookup(name => name switch
            {
                AuthSmokeSettings.ApiBaseUrlEnvironmentVariableName => "https://api.example.com/platform",
                AuthSmokeSettings.StorefrontBaseUrlEnvironmentVariableName => "https://shop.example.com/storefront",
                AuthSmokeSettings.ClientAppBaseUrlEnvironmentVariableName => "https://account.example.com/app",
                AuthSmokeSettings.AllowInvalidCertificateEnvironmentVariableName => "true",
                _ => null,
            });

            Assert.True(settings.IsEnabled);
            Assert.True(settings.AllowInvalidCertificate);
            Assert.Equal("https://api.example.com/platform/api/authentication/login", settings.ResolveAuthenticationUrl("login"));
            Assert.Equal("https://shop.example.com/storefront/checkout", settings.ResolveStorefrontUrl("/checkout"));
            Assert.Equal("checkout", settings.ToStorefrontRequestTarget("/checkout"));
            Assert.Equal("https://account.example.com/app/account/checkout", settings.ResolveClientAppUrl("/account/checkout"));
        }

        [Fact]
        public void ResolveClientAppUrl_WhenRoutePathIsAbsolute_Throws()
        {
            var settings = AuthSmokeSettings.FromLookup(name => name switch
            {
                AuthSmokeSettings.ApiBaseUrlEnvironmentVariableName => "https://api.example.com",
                AuthSmokeSettings.StorefrontBaseUrlEnvironmentVariableName => "https://shop.example.com",
                AuthSmokeSettings.ClientAppBaseUrlEnvironmentVariableName => "https://account.example.com",
                _ => null,
            });

            var exception = Assert.Throws<InvalidOperationException>(() => settings.ResolveClientAppUrl("file:///account/checkout"));

            Assert.Contains("route path", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void FromLookup_WhenApiBaseAlreadyTargetsAuthenticationRoot_PreservesIt()
        {
            var settings = AuthSmokeSettings.FromLookup(name => name switch
            {
                AuthSmokeSettings.ApiBaseUrlEnvironmentVariableName => "https://api.example.com/platform/api/authentication",
                AuthSmokeSettings.StorefrontBaseUrlEnvironmentVariableName => "https://shop.example.com",
                AuthSmokeSettings.ClientAppBaseUrlEnvironmentVariableName => "https://account.example.com",
                _ => null,
            });

            Assert.Equal("https://api.example.com/platform/api/authentication/login", settings.ResolveAuthenticationUrl("login"));
        }

        [Fact]
        public void FromLookup_WhenOnlySomeBaseUrlsAreConfigured_Throws()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => AuthSmokeSettings.FromLookup(name => name switch
            {
                AuthSmokeSettings.ApiBaseUrlEnvironmentVariableName => "https://api.example.com",
                AuthSmokeSettings.StorefrontBaseUrlEnvironmentVariableName => "https://shop.example.com",
                _ => null,
            }));

            Assert.Contains(AuthSmokeSettings.ClientAppBaseUrlEnvironmentVariableName, exception.Message, StringComparison.Ordinal);
        }
    }
}
