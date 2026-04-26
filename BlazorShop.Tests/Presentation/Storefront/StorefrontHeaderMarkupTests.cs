namespace BlazorShop.Tests.Presentation.Storefront
{
    using Xunit;

    public class StorefrontHeaderMarkupTests
    {
        [Fact]
        public void StorefrontHeader_RendersPublicShopNavigation()
        {
            var markup = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/Components/Layout/StorefrontHeader.razor");

            Assert.Contains("BLAZORSHOP", markup);
            Assert.Contains("PUBLIC SHOP", markup);
            Assert.Contains("Home", markup);
            Assert.Contains("New Releases", markup);
            Assert.Contains("Today's Deals", markup);
            Assert.Contains("About", markup);
            Assert.Contains("Customer Service", markup);
        }

        [Fact]
        public void StorefrontHeader_DoesNotRenderWorkspaceNavigation()
        {
            var markup = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/Components/Layout/StorefrontHeader.razor");

            Assert.DoesNotContain("Admin Operations", markup);
            Assert.DoesNotContain("ADMIN OPERATIONS", markup);
            Assert.DoesNotContain("CUSTOMER ACCOUNT", markup);
            Assert.DoesNotContain("Back to shop", markup);
            Assert.DoesNotContain("Dashboard", markup);
            Assert.DoesNotContain("Orders", markup);
            Assert.DoesNotContain("Products", markup);
            Assert.DoesNotContain("WorkspaceHeader", markup);
        }

        [Fact]
        public void StorefrontLayout_UsesDedicatedStorefrontHeader()
        {
            var markup = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/Components/Layout/MainLayout.razor");

            Assert.Contains("<StorefrontHeader />", markup);
            Assert.DoesNotContain("WorkspaceHeader", markup);
            Assert.DoesNotContain("Back to shop", markup);
            Assert.DoesNotContain("clip-diagonal", markup);
        }

        [Theory]
        [InlineData("BlazorShop.Presentation/BlazorShop.Web/Layout/AccountLayout.razor")]
        [InlineData("BlazorShop.Presentation/BlazorShop.Web/Layout/AdminLayout.razor")]
        public void WorkspaceLayouts_KeepBackToShopAction(string relativePath)
        {
            var markup = ReadRepositoryFile(relativePath);

            Assert.Contains("WorkspaceHeader", markup);
            Assert.Contains("Back to shop", markup);
            Assert.Contains("ShopHref", markup);
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
