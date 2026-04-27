namespace BlazorShop.Tests.Presentation.Storefront
{
    using Xunit;

    public class StorefrontHeaderMarkupTests
    {
        [Fact]
        public void StorefrontHeader_RendersPublicShopNavigation()
        {
            var markup = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/Components/Layout/StorefrontHeader.razor");

            Assert.Contains("<header class=\"bs-storefront-header relative text-neutral-100\">", markup);
            Assert.Contains("bs-storefront-header__shell", markup);
            Assert.Contains("bs-storefront-header__desktop", markup);
            Assert.Contains("bs-storefront-header__mobile", markup);
            Assert.DoesNotContain("bs-storefront-header__backdrop", markup);
            Assert.DoesNotContain("HeaderShell", markup);
            Assert.DoesNotContain("@<>", markup);
            Assert.DoesNotContain("</>", markup);
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
        public void WorkspaceHeader_UsesCanonicalWorkspaceShell()
        {
            var workspace = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Web/Layout/WorkspaceHeader.razor");

            Assert.Contains("<header class=\"bs-workspace-header relative text-neutral-100\">", workspace);
            Assert.Contains("bs-workspace-header__backdrop", workspace);
            Assert.Contains("bs-workspace-header__desktop", workspace);
            Assert.Contains("bs-workspace-header__mobile", workspace);
            Assert.DoesNotContain("HeaderShell", workspace);
        }

        [Fact]
        public void StorefrontCss_CopiesWorkspaceShellPatternWithoutSharedGlobalHeader()
        {
            var styles = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Storefront/wwwroot/css/storefront.css");

            Assert.Contains("background: transparent", styles);
            Assert.Contains(".bs-storefront-header__shell", styles);
            Assert.Contains(".bs-storefront-header__shell::before", styles);
            Assert.Contains(".bs-storefront-header__desktop", styles);
            Assert.Contains(".bs-storefront-header__mobile", styles);
            Assert.Contains("rgba(23, 23, 23, 0.98)", styles);
            Assert.Contains("width: min(calc(100% - 2rem), 80rem)", styles);
            Assert.Contains("clip-path: polygon(0 0, 100% 0, 95% 100%, 5% 100%)", styles);
            Assert.Contains("padding-left: clamp(4.5rem, 6vw, 5.5rem)", styles);
            Assert.Contains("@media (min-width: 1024px)", styles);
            Assert.Contains("clip-path: none;", styles);
            Assert.DoesNotContain(".bs-storefront-header__backdrop", styles);
            Assert.DoesNotContain("inset: 0 auto 0 50%", styles);
            Assert.DoesNotContain("transform: translateX(-50%)", styles);
            Assert.DoesNotContain("bs-header-shell", styles);
            Assert.DoesNotContain("background: rgba(255, 255, 255, 0.88)", styles);
            Assert.DoesNotContain("background: #0a0a0a", styles);
            Assert.DoesNotContain("linear-gradient(135deg", styles);
            Assert.DoesNotContain("box-shadow: 0 14px 30px", styles);
            Assert.DoesNotContain("box-shadow: 0 20px 48px", styles);
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
