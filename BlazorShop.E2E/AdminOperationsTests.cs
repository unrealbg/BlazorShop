namespace BlazorShop.E2E;

using System.Text.RegularExpressions;

using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

[Collection(E2ECollection.Name)]
public sealed class AdminOperationsTests(E2EApplicationFixture application) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new() { IgnoreHTTPSErrors = true, Locale = "en-US" };

    [Fact]
    public async Task AdminScreens_ResponsiveNavigationAndData()
    {
        var data = await application.SeedScenarioAsync();
        await application.GrantScenarioAdminAsync(data);
        await application.SeedAccountOrderAsync(data);
        try
        {
            await LoginAsync(data);
            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                await Page.SetViewportSizeAsync(width, 1000);
                foreach (var path in new[] { "admin", "admin/products", "admin/categories", "admin/inventory", "admin/orders", "admin/users", "admin/seo", "admin/redirects", "admin/settings", "admin/audit" })
                {
                    await GoAsync(path);
                    await Expect(Page.Locator("main h1, main h2").First).ToBeVisibleAsync();
                    await Expect(Page.Locator("main").GetByText(new Regex("^Loading ", RegexOptions.IgnoreCase))).ToHaveCountAsync(0);
                    await CaptureAsync(path.Replace('/', '-'), width);
                }
            }
        }
        finally { await application.RemoveScenarioAsync(data); }
    }

    private async Task LoginAsync(E2ETestData data)
    {
        await GoAsync("authentication/login");
        await Page.GetByLabel("Email Address", new() { Exact = true }).FillAsync(data.UserEmail);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(data.UserPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login", Exact = true }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex("/admin$"));
    }

    private Task GoAsync(string path) => Page.GotoAsync(new Uri(application.WebBaseUrl, path).ToString());

    private async Task CaptureAsync(string state, int width)
    {
        await Page.EvaluateAsync("document.fonts.ready");
        var directory = Path.Combine(application.ArtifactDirectory, "ui-v2-admin-before");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"{state}-{width}.png"), FullPage = true, Mask = [Page.Locator("input[type='password']")] });
    }
}
