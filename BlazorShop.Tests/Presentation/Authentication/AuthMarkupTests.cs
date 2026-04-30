namespace BlazorShop.Tests.Presentation.Authentication
{
    using Xunit;

    public class AuthMarkupTests
    {
        [Fact]
        public void LoginPage_UsesFocusedDesktopAuthMeasure()
        {
            var markup = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Web/Authentication/Components/Login.razor");

            AssertUsesFocusedAuthMeasure(markup);
        }

        [Fact]
        public void RegisterPage_UsesFocusedDesktopAuthMeasure()
        {
            var markup = ReadRepositoryFile("BlazorShop.Presentation/BlazorShop.Web/Authentication/Components/Register.razor");

            AssertUsesFocusedAuthMeasure(markup);
            Assert.DoesNotContain("xl:grid-cols-[minmax(0,1fr)_20rem]", markup);
            Assert.DoesNotContain("bs-page-hero mb-8", markup);
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

        private static void AssertUsesFocusedAuthMeasure(string markup)
        {
            Assert.Contains("<section class=\"w-full px-4 py-8 sm:px-6 lg:px-8 lg:pt-5 lg:pb-7\">", markup);
            Assert.Contains("mx-auto grid w-full max-w-[66rem] items-start gap-7", markup);
            Assert.Contains("lg:grid-cols-[minmax(0,1fr)_18rem] lg:gap-8", markup);
            Assert.Contains("xl:w-fit xl:max-w-full xl:grid-cols-[42rem_20rem] xl:justify-center", markup);
            Assert.Contains("flex flex-col gap-5 sm:flex-row sm:items-start sm:justify-between", markup);
            Assert.Contains("class=\"bs-button bs-button-secondary shrink-0 whitespace-nowrap\"", markup);
            Assert.Contains("mx-auto max-w-lg", markup);
            Assert.Contains("mx-auto flex max-w-lg", markup);
            Assert.DoesNotContain("<section class=\"mx-auto w-full", markup);
            Assert.DoesNotContain("max-w-6xl", markup);
            Assert.DoesNotContain("max-w-[62rem]", markup);
            Assert.DoesNotContain("xl:max-w-[68rem]", markup);
            Assert.DoesNotContain("lg:grid-cols-[minmax(0,1fr)_16rem]", markup);
            Assert.DoesNotContain("lg:grid-cols-[minmax(0,2fr)_minmax(16rem,1fr)]", markup);
            Assert.Contains("aside class=\"space-y-5\"", markup);
        }
    }
}
