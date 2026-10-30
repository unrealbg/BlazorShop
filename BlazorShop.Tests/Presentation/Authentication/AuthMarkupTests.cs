namespace BlazorShop.Tests.Presentation.Authentication
{
    using Xunit;

    public class AuthMarkupTests
    {
        [Fact]
        public void LoginPage_PreservesValidatedSubmitAndCheckoutHandoffRoute()
        {
            var markup = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Web/Authentication/Components/Login.razor");

            Assert.Contains("@page \"/authentication/login/{*route}\"", markup);
            Assert.Contains("<EditForm Model=\"User\" OnValidSubmit=\"LoginUser\"", markup);
            Assert.Contains("<DataAnnotationsValidator />", markup);
            Assert.Contains("disabled=\"@IsSubmitting\"", markup);
            Assert.Contains("StartDemo(\"User\")", markup);
            Assert.Contains("StartDemo(\"Admin\")", markup);
        }

        [Fact]
        public void RegisterPage_PreservesValidatedSubmitAndSubmissionGate()
        {
            var markup = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Web/Authentication/Components/Register.razor");

            Assert.Contains("<EditForm Model=\"_user\" OnValidSubmit=\"HandleRegister\"", markup);
            Assert.Contains("<DataAnnotationsValidator />", markup);
            Assert.Contains("disabled=\"@_isLoading\"", markup);
            Assert.Contains("_user.ConfirmPassword", markup);
        }

        [Fact]
        public void AuthControls_KeepMobileTouchTargets()
        {
            var styles = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Web/wwwroot/css/app.css");

            Assert.Contains("@media (max-width: 639.98px)", styles);
            Assert.Contains("min-height: 44px !important;", styles);
        }

        [Fact]
        public void FloatingTextInput_AssociatesVisibleLabelWithInput()
        {
            var component = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Web/Components/FormControls/FloatingTextInput.cs");

            Assert.Contains("builder.AddAttribute(4, \"id\", resolvedInputId);", component);
            Assert.Contains("builder.AddAttribute(12, \"for\", resolvedInputId);", component);
        }

        [Fact]
        public void WebShell_VersionsAssetsAndRevalidatesStaticFiles()
        {
            var index = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Web/wwwroot/index.html");
            var nginx = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Web/nginx.conf");

            Assert.Contains("css/site.css?v=mobile-20260801", index);
            Assert.Contains("css/app.css?v=mobile-20260801", index);
            Assert.Contains("js/app.js?v=mobile-20260801", index);
            Assert.Contains("location = /index.html", nginx);
            Assert.Contains("location ~* \\.(css|js)$", nginx);
            Assert.Contains("Cache-Control \"no-cache\"", nginx);
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
