namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;

    using Xunit;

    [Trait("Category", "SeoSmoke")]
    public class StorefrontSeoSmokeTests
    {
        [Fact]
        public async Task HomePage_Returns200WithExpectedCanonicalAndStructuredData()
        {
            using var smokeClient = CreateSmokeClientOrSkip();
            if (smokeClient is null)
            {
                return;
            }

            using var response = await smokeClient.GetAsync(smokeClient.Settings.HomePath);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            StorefrontSeoSmokeAssertions.AssertIndexableHtmlPage(
                response,
                document,
                smokeClient.Settings.ResolveAbsoluteUrl(smokeClient.Settings.HomePath),
                "Organization",
                "WebSite");
        }

        [Fact]
        public async Task StaticInformationalPage_Returns200WithExpectedCanonicalAndStructuredData()
        {
            using var smokeClient = CreateSmokeClientOrSkip();
            if (smokeClient is null)
            {
                return;
            }

            using var response = await smokeClient.GetAsync(smokeClient.Settings.StaticPagePath);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            StorefrontSeoSmokeAssertions.AssertIndexableHtmlPage(
                response,
                document,
                smokeClient.Settings.ResolveAbsoluteUrl(smokeClient.Settings.StaticPagePath),
                "WebPage");
        }

        [Fact]
        public async Task CategoryPage_Returns200WithExpectedCanonicalAndStructuredData()
        {
            using var smokeClient = CreateSmokeClientOrSkip();
            if (smokeClient is null)
            {
                return;
            }

            using var response = await smokeClient.GetAsync(smokeClient.Settings.CategoryPath);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            StorefrontSeoSmokeAssertions.AssertIndexableHtmlPage(
                response,
                document,
                smokeClient.Settings.ResolveAbsoluteUrl(smokeClient.Settings.CategoryPath),
                "CollectionPage",
                "BreadcrumbList");
        }

        [Fact]
        public async Task ProductPage_Returns200WithExpectedCanonicalAndStructuredData()
        {
            using var smokeClient = CreateSmokeClientOrSkip();
            if (smokeClient is null)
            {
                return;
            }

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

        [Fact]
        public async Task MissingRoute_Returns404WithoutCanonicalAndWithNoindexProtection()
        {
            using var smokeClient = CreateSmokeClientOrSkip();
            if (smokeClient is null)
            {
                return;
            }

            using var response = await smokeClient.GetAsync(smokeClient.Settings.MissingPath);
            var document = await StorefrontHtmlAuditDocument.CreateAsync(response);

            StorefrontSeoSmokeAssertions.AssertNoindexedErrorSurface(response, document, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Sitemap_ReturnsXmlAndContainsCriticalRoutes()
        {
            using var smokeClient = CreateSmokeClientOrSkip();
            if (smokeClient is null)
            {
                return;
            }

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

        [Fact]
        public async Task RobotsTxt_IsAvailableAndReferencesSitemap()
        {
            using var smokeClient = CreateSmokeClientOrSkip();
            if (smokeClient is null)
            {
                return;
            }

            using var response = await smokeClient.GetAsync("/robots.txt");
            var content = await response.Content.ReadAsStringAsync();

            StorefrontSeoSmokeAssertions.AssertRobotsDocument(
                response,
                content,
                smokeClient.Settings.ResolveAbsoluteUrl("/sitemap.xml"));
        }

        [Fact]
        public async Task RedirectSource_ReturnsExpectedSingleHopCanonicalTarget()
        {
            using var smokeClient = CreateSmokeClientOrSkip(requireRedirect: true);
            if (smokeClient is null)
            {
                return;
            }

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

        private static StorefrontSeoSmokeClient? CreateSmokeClientOrSkip(bool requireRedirect = false)
        {
            var settings = StorefrontSeoSmokeSettings.FromEnvironment();
            if (!settings.IsEnabled)
            {
                Assert.False(
                    settings.RequireConfiguration,
                    $"Set {StorefrontSeoSmokeSettings.BaseUrlEnvironmentVariableName} to run the storefront SEO smoke suite.");
                return null;
            }

            if (requireRedirect && !settings.HasRedirectExpectation)
            {
                return null;
            }

            return new StorefrontSeoSmokeClient(settings);
        }
    }
}