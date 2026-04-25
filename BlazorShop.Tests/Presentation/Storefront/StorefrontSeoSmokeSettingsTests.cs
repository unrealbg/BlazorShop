namespace BlazorShop.Tests.Presentation.Storefront
{
    using Xunit;

    public class StorefrontSeoSmokeSettingsTests
    {
        [Fact]
        public void FromLookup_WhenBaseUrlIsMissing_DisablesSmokeSuite()
        {
            var settings = StorefrontSeoSmokeSettings.FromLookup(_ => null);

            Assert.False(settings.IsEnabled);
            Assert.Equal("/about-us", settings.StaticPagePath);
            Assert.True(settings.HasRedirectExpectation);
        }

        [Fact]
        public void FromLookup_WhenConfigured_ResolvesPathsAgainstBaseUrlPathPrefix()
        {
            var settings = StorefrontSeoSmokeSettings.FromLookup(name => name switch
            {
                StorefrontSeoSmokeSettings.BaseUrlEnvironmentVariableName => "https://shop.example.com/storefront",
                StorefrontSeoSmokeSettings.AllowInvalidCertificateEnvironmentVariableName => "true",
                _ => null,
            });

            Assert.True(settings.IsEnabled);
            Assert.True(settings.AllowInvalidCertificate);
            Assert.Equal("https://shop.example.com/storefront/", settings.ResolveAbsoluteUrl(settings.HomePath));
            Assert.Equal("https://shop.example.com/storefront/category/sneakers", settings.ResolveAbsoluteUrl(settings.CategoryPath));
            Assert.Equal("category/sneakers", settings.ToRequestTarget(settings.CategoryPath));
        }

        [Fact]
        public void FromLookup_WhenRedirectPairIsBlank_DisablesRedirectSmokeOnly()
        {
            var settings = StorefrontSeoSmokeSettings.FromLookup(name => name switch
            {
                StorefrontSeoSmokeSettings.BaseUrlEnvironmentVariableName => "https://shop.example.com",
                StorefrontSeoSmokeSettings.RedirectSourcePathEnvironmentVariableName => string.Empty,
                StorefrontSeoSmokeSettings.RedirectTargetPathEnvironmentVariableName => string.Empty,
                _ => null,
            });

            Assert.True(settings.IsEnabled);
            Assert.False(settings.HasRedirectExpectation);
            Assert.Null(settings.RedirectSourcePath);
            Assert.Null(settings.RedirectTargetPath);
        }

        [Fact]
        public void FromLookup_WhenRedirectPairIsPartial_Throws()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => StorefrontSeoSmokeSettings.FromLookup(name => name switch
            {
                StorefrontSeoSmokeSettings.BaseUrlEnvironmentVariableName => "https://shop.example.com",
                StorefrontSeoSmokeSettings.RedirectSourcePathEnvironmentVariableName => "/product/legacy-runner",
                StorefrontSeoSmokeSettings.RedirectTargetPathEnvironmentVariableName => string.Empty,
                _ => null,
            }));

            Assert.Contains(StorefrontSeoSmokeSettings.RedirectSourcePathEnvironmentVariableName, exception.Message, StringComparison.Ordinal);
        }
    }
}