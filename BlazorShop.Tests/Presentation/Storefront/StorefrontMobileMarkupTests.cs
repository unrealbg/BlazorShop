namespace BlazorShop.Tests.Presentation.Storefront
{
    using Xunit;

    public class StorefrontMobileMarkupTests
    {
        [Fact]
        public void HomePage_UsesCompactMobileSpacing()
        {
            var hero = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/Components/Public/HeroBanner.razor");
            var home = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/Pages/Home.razor");

            var styles = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/wwwroot/css/ui-v2-storefront.css");

            Assert.Contains("bs-v2-hero-actions", hero);
            Assert.Contains("aria-labelledby=\"storefront-hero-heading\"", hero);
            Assert.Contains("width=\"760\" height=\"460\"", hero);
            Assert.Contains("bs-v2-category-grid", home);
            Assert.Contains("@media (max-width: 640px)", styles);
            Assert.Contains(".bs-v2-category-grid { grid-template-columns: 1fr; }", styles);
            Assert.Contains("min-height: 44px;", styles);
        }

        [Fact]
        public void StorefrontApp_VersionsBrowserCachedAssets()
        {
            var app = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/App.razor");

            Assert.Contains("VersionedAsset(\"css/site.css\")", app);
            Assert.Contains("VersionedAsset(\"css/storefront.css\")", app);
            Assert.Contains("VersionedAsset(\"css/ui-v2-storefront.css\")", app);
            Assert.Contains("VersionedAsset(\"js/storefrontCommerce.js\")", app);
            Assert.Contains("fileInfo.LastModified.UtcDateTime.Ticks", app);
        }

        [Fact]
        public void ProductExperiences_UseMobileFriendlyControlsAndDensity()
        {
            var card = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/Components/Catalog/ProductCard.razor");
            var categoryPage = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/Pages/CategoryPage.razor");
            var productPage = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/Pages/ProductPage.razor");
            var cartPage = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/Pages/CartPage.razor");

            Assert.Contains("bs-v2-product-actions", card);
            Assert.Contains("data-storefront-add-to-cart", card);
            Assert.Contains("data-unit-price=\"@UnitPriceValue\"", card);
            Assert.Contains("#purchase", card);
            Assert.Contains("No image available", card);
            Assert.Contains("bs-v2-detail-layout", productPage);
            Assert.Contains("bs-v2-detail-buy-actions", productPage);
            Assert.Contains("bs-v2-detail-field", productPage);
            Assert.Contains("bs-v2-category-intro", categoryPage);
            Assert.Contains("bs-v2-category-toolbar", categoryPage);
            Assert.Contains("p-5 shadow-lg sm:p-8", cartPage);
            Assert.Contains("min-h-11 w-full", cartPage);
        }

        [Fact]
        public void StorefrontShell_UsesTouchTargetsAndCompactFooterColumns()
        {
            var footer = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/Components/Layout/StorefrontFooter.razor");
            var styles = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/wwwroot/css/ui-v2-storefront.css");

            Assert.Contains("aria-label=\"Footer shop navigation\"", footer);
            Assert.Contains("href=\"@StorefrontRoutes.Privacy\"", footer);
            Assert.Contains("href=\"@StorefrontRoutes.Terms\"", footer);
            Assert.Contains(".bs-v2-footer-grid { grid-template-columns: 1fr; }", styles);
            Assert.Contains("min-height: 44px;", styles);
        }

        private static string ReadRepositoryFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "BlazorShop.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Unable to locate BlazorShop.sln from the test output directory.");
        }
    }
}
