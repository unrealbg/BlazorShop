namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;

    using Xunit;

    [Trait("Category", "SeoSmoke")]
    public class StorefrontSeoSmokeTests
    {
        [StorefrontSeoSmokeFact]
        public async Task HomePage_Returns200WithExpectedCanonicalAndStructuredData()
        {
            using var smokeClient = CreateSmokeClientOrFail();

            using var response = await smokeClient.GetAsync(smokeClient.Settings.HomePath);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            StorefrontSeoSmokeAssertions.AssertIndexableHtmlPage(
                response,
                document,
                smokeClient.Settings.ResolveAbsoluteUrl(smokeClient.Settings.HomePath),
                "Organization",
                "WebSite");
        }

        [StorefrontSeoSmokeFact]
        public async Task StaticInformationalPage_Returns200WithExpectedCanonicalAndStructuredData()
        {
            using var smokeClient = CreateSmokeClientOrFail();

            using var response = await smokeClient.GetAsync(smokeClient.Settings.StaticPagePath);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            StorefrontSeoSmokeAssertions.AssertIndexableHtmlPage(
                response,
                document,
                smokeClient.Settings.ResolveAbsoluteUrl(smokeClient.Settings.StaticPagePath),
                "WebPage");
        }

        [StorefrontSeoSmokeFact]
        public async Task CategoryPage_Returns200WithExpectedCanonicalAndStructuredData()
        {
            using var smokeClient = CreateSmokeClientOrFail();

            using var response = await smokeClient.GetAsync(smokeClient.Settings.CategoryPath);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            StorefrontSeoSmokeAssertions.AssertIndexableHtmlPage(
                response,
                document,
                smokeClient.Settings.ResolveAbsoluteUrl(smokeClient.Settings.CategoryPath),
                "CollectionPage",
                "BreadcrumbList");
        }

        [StorefrontSeoSmokeFact]
        public async Task ProductPage_Returns200WithExpectedCanonicalAndStructuredData()
        {
            using var smokeClient = CreateSmokeClientOrFail();

            using var response = await smokeClient.GetAsync(smokeClient.Settings.ProductPath);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            StorefrontSeoSmokeAssertions.AssertIndexableHtmlPage(
                response,
                document,
                smokeClient.Settings.ResolveAbsoluteUrl(smokeClient.Settings.ProductPath),
                "Product",
                "Offer",
                "BreadcrumbList");
        }

        [StorefrontSeoSmokeFact]
        public async Task MissingRoute_Returns404WithoutCanonicalAndWithNoindexProtection()
        {
            using var smokeClient = CreateSmokeClientOrFail();

            using var response = await smokeClient.GetAsync(smokeClient.Settings.MissingPath);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            StorefrontSeoSmokeAssertions.AssertNoindexedErrorSurface(response, document, HttpStatusCode.NotFound);
        }

        [StorefrontSeoSmokeFact]
        public async Task Sitemap_ReturnsXmlAndContainsCriticalRoutes()
        {
            using var smokeClient = CreateSmokeClientOrFail();

            using var response = await smokeClient.GetAsync("/sitemap.xml");
            var document = await StorefrontSitemapAuditDocument.CreateAsync(response);

            StorefrontSeoSmokeAssertions.AssertSitemapDocument(
                response,
                document,
                smokeClient.Settings.ResolveAbsoluteUrl(smokeClient.Settings.HomePath),
                smokeClient.Settings.ResolveAbsoluteUrl(smokeClient.Settings.StaticPagePath),
                smokeClient.Settings.ResolveAbsoluteUrl(smokeClient.Settings.CategoryPath),
                smokeClient.Settings.ResolveAbsoluteUrl(smokeClient.Settings.ProductPath));
        }

        [StorefrontSeoSmokeFact]
        public async Task RobotsTxt_IsAvailableAndReferencesSitemap()
        {
            using var smokeClient = CreateSmokeClientOrFail();

            using var response = await smokeClient.GetAsync("/robots.txt");
            var content = await response.Content.ReadAsStringAsync();

            StorefrontSeoSmokeAssertions.AssertRobotsDocument(
                response,
                content,
                smokeClient.Settings.ResolveAbsoluteUrl("/sitemap.xml"));
        }

        [StorefrontSeoSmokeFact(requireRedirect: true)]
        public async Task RedirectSource_ReturnsExpectedSingleHopCanonicalTarget()
        {
            using var smokeClient = CreateSmokeClientOrFail(requireRedirect: true);

            using var redirectResponse = await smokeClient.GetAsync(smokeClient.Settings.RedirectSourcePath!);
            StorefrontSeoSmokeAssertions.AssertPermanentRedirect(
                redirectResponse,
                smokeClient.Settings.RedirectTargetPath!,
                smokeClient.Settings.RedirectExpectedStatusCode);

            using var finalResponse = await smokeClient.GetAsync(smokeClient.Settings.RedirectTargetPath!);
            var finalDocument = await StorefrontHtmlAuditDocument.CreateAsync(finalResponse);

            StorefrontSeoSmokeAssertions.AssertCanonicalizedHtmlPage(
                finalResponse,
                finalDocument,
                smokeClient.Settings.ResolveExpectedCanonicalUrl(smokeClient.Settings.RedirectTargetPath!));
        }

        private static StorefrontSeoSmokeClient CreateSmokeClientOrFail(bool requireRedirect = false)
        {
            var settings = StorefrontSeoSmokeSettings.FromEnvironment();
            Assert.True(settings.IsEnabled, $"Set {StorefrontSeoSmokeSettings.BaseUrlEnvironmentVariableName} to run the storefront SEO smoke suite.");

            if (requireRedirect)
            {
                Assert.True(
                    settings.HasRedirectExpectation,
                    $"Set both {StorefrontSeoSmokeSettings.RedirectSourcePathEnvironmentVariableName} and {StorefrontSeoSmokeSettings.RedirectTargetPathEnvironmentVariableName}, or leave them unset to use the default redirect smoke route.");
            }

            return new StorefrontSeoSmokeClient(settings);
        }
    }
}