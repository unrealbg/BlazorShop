namespace BlazorShop.E2E;

using System.Text.RegularExpressions;

using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

[Collection(E2ECollection.Name)]
public sealed partial class AdminOperationsTests(E2EApplicationFixture application) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new() { IgnoreHTTPSErrors = true, Locale = "en-US", Permissions = ["clipboard-read", "clipboard-write"] };

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
                    await Expect(Page.Locator("main h1")).ToBeVisibleAsync();
                    await Expect(Page.Locator("main").GetByText(new Regex("^Loading ", RegexOptions.IgnoreCase))).ToHaveCountAsync(0);
                    await Expect(Page.Locator("main [role=alert]")).ToHaveCountAsync(0);
                    if (path == "admin/products")
                    {
                        await Page.GetByLabel("Search products...").FillAsync(data.CategoryName.Split(' ')[^1]);
                        await Page.GetByRole(AriaRole.Heading, new() { Level = 1 }).ClickAsync();
                        await Expect(Button(Page.Locator("main"), "Collapse All")).ToBeVisibleAsync();
                        await Expect(Page.GetByRole(AriaRole.Row).Filter(new() { HasText = data.SimpleProductName })).ToBeVisibleAsync();
                    }
                    if (path == "admin/inventory") await FilterInventoryAsync(data.CategoryName.Split(' ')[^1]);
                    await AssertFitsAsync();
                    await CaptureAsync(path.Replace('/', '-'), width);
                }
                await GoAsync("admin/categories");
                var add = Page.GetByRole(AriaRole.Button, new() { Name = "Add Category", Exact = true });
                await add.ClickAsync();
                await Expect(Dialog.GetByLabel("Category Name")).ToBeFocusedAsync();
                await Dialog.GetByRole(AriaRole.Button, new() { Name = "Save", Exact = true }).ClickAsync();
                var errorId = await Dialog.GetByLabel("Category Name").GetAttributeAsync("aria-describedby");
                Assert.False(string.IsNullOrWhiteSpace(errorId));
                await Expect(Dialog.Locator($"#{errorId} .validation-message")).ToHaveTextAsync("The Category Name field is required.");
                await Expect(Dialog.GetByRole(AriaRole.Alert)).ToContainTextAsync("The Category Name field is required.");
                for (var tab = 0; tab < 7; tab++)
                {
                    await Page.Keyboard.PressAsync("Tab");
                    Assert.True(await Dialog.EvaluateAsync<bool>("d => d.contains(document.activeElement)"));
                }
                await AssertFitsAsync();
                await CaptureAsync("category-validation-dialog", width);
                await Page.Keyboard.PressAsync("Escape");
                await Expect(Dialog).ToHaveCountAsync(0);
                await Expect(add).ToBeFocusedAsync();
                if (width < 1100)
                {
                    await Page.Locator(".bs-workspace-header__menu summary").ClickAsync();
                    await Page.Locator(".bs-workspace-header__mobile-panel").GetByRole(AriaRole.Link, new() { Name = "Inventory", Exact = true }).ClickAsync();
                    await Expect(Page.Locator(".bs-workspace-header__menu")).Not.ToHaveAttributeAsync("open", "");
                    await Expect(Page.Locator("main h1")).ToHaveTextAsync("Inventory");
                }
                await Page.Locator(".v2-admin-skip").FocusAsync();
                await Page.Keyboard.PressAsync("Enter");
                await Expect(Page.Locator("#admin-content")).ToBeFocusedAsync();
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
        var directory = Path.Combine(application.ArtifactDirectory, "ui-v2-admin");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"{state}-{width}.png"), FullPage = await Dialog.CountAsync() == 0, Mask = [Page.Locator("input[type='password']"), Page.Locator("dialog[open] pre"), Page.Locator("dialog[open] div:has(> dt:text-is('IP')) dd, dialog[open] div:has(> dt:text-is('User agent')) dd"), Page.Locator("#admin-settings-9, #admin-settings-10")] });
    }

    private ILocator Dialog => Page.Locator("dialog[open]");
    private ILocator Row(string text) => Page.GetByRole(AriaRole.Row).Filter(new() { HasText = text });
    private static ILocator Button(ILocator scope, string text) => scope.GetByRole(AriaRole.Button, new() { Name = text, Exact = true });
    private async Task FilterInventoryAsync(string text)
    {
        await Page.GetByLabel("Search product or SKU").FillAsync(text);
        await Button(Page.Locator("main"), "Apply").ClickAsync();
        await Expect(Page.GetByText("Loading inventory...", new() { Exact = true })).ToHaveCountAsync(0);
    }

    private async Task AssertFitsAsync()
    {
        Assert.True(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth + 1"), "Admin page overflows horizontally.");
        var clipped = await Page.Locator("main .bs-table-shell, dialog[open]").EvaluateAllAsync<string[]>("els => els.filter(e => e.scrollWidth > e.clientWidth + 2).map(e => e.tagName + ':' + e.className)");
        Assert.Empty(clipped);
    }

    private async Task CaptureWidthsAsync(string state)
    {
        foreach (var width in new[] { 320, 390, 768, 1440 })
        {
            await Page.SetViewportSizeAsync(width, 1000);
            await Dialog.EvaluateAllAsync<bool>("dialogs => { dialogs.forEach(dialog => dialog.scrollTop = 0); return true; }");
            await AssertFitsAsync();
            await CaptureAsync(state, width);
        }
    }

    private async Task<Dictionary<string, string>> HeadersAsync() => new() { ["Authorization"] = $"Bearer {await Page.EvaluateAsync<string>("sessionStorage.getItem('token')")}" };
}
