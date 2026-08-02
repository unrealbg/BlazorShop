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

            Assert.Contains("py-10 text-center sm:py-14", hero);
            Assert.Contains("text-3xl", hero);
            Assert.Contains("grid grid-cols-2 gap-3", hero);
            Assert.Contains("min-h-11 w-full", hero);
            Assert.Contains("pb-8 pt-8", home);
            Assert.Contains("mt-6 grid gap-4 sm:mt-8", home);
        }

        [Fact]
        public void StorefrontApp_VersionsBrowserCachedAssets()
        {
            var app = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/App.razor");

            Assert.Contains("VersionedAsset(\"css/site.css\")", app);
            Assert.Contains("VersionedAsset(\"css/storefront.css\")", app);
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

            Assert.Contains("p-5 sm:p-6", card);
            Assert.Contains("grid grid-cols-2 gap-2 sm:flex", card);
            Assert.Contains("min-h-11 w-full", card);
            Assert.Contains("p-5 shadow-lg sm:p-8", productPage);
            Assert.Contains("text-3xl font-extrabold", productPage);
            Assert.Contains("grid grid-cols-2 gap-3 sm:flex", productPage);
            Assert.Contains("p-5 shadow-lg sm:p-8", categoryPage);
            Assert.Contains("text-3xl font-extrabold", categoryPage);
            Assert.Contains("p-5 shadow-lg sm:p-8", cartPage);
            Assert.Contains("min-h-11 w-full", cartPage);
        }

        [Fact]
        public void StorefrontShell_UsesTouchTargetsAndCompactFooterColumns()
        {
            var layout = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/Components/Layout/MainLayout.razor");
            var styles = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/wwwroot/css/storefront.css");

            Assert.Contains("grid grid-cols-2 gap-8 lg:grid-cols-4", layout);
            Assert.Contains("col-span-2 lg:col-span-1", layout);
            Assert.Contains("@media (max-width: 1023.98px)", styles);
            Assert.Contains("min-height: 2.75rem;", styles);
            Assert.Contains("width: 2.75rem;", styles);
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
