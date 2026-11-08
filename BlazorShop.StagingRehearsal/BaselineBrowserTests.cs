namespace BlazorShop.StagingRehearsal;

using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

public sealed class BaselineBrowserTests : PageTest
{
    [Fact]
    [Trait("Category", "StagingRehearsal")]
    public async Task BaselineCatalogVariantAndProtectedOrderHistory()
    {
        var configuration = RehearsalConfiguration.Load(baseline: true);
        Page.SetDefaultTimeout(20_000);
        await Page.GotoAsync(new Uri(configuration.StorefrontBaseUrl, $"product/{configuration.VariantProductSlug}").ToString());
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = configuration.VariantProductName, Exact = true }))
            .ToBeVisibleAsync();
        var variants = Page.GetByLabel("Choose a variant", new() { Exact = true });
        await variants.SelectOptionAsync(configuration.VariantId.ToString("D"));
        await Expect(variants).ToHaveValueAsync(configuration.VariantId.ToString("D"));

        await Page.GotoAsync(new Uri(configuration.WebBaseUrl, "authentication/login/account/orders").ToString());
        await Page.GetByLabel("Email Address", new() { Exact = true }).FillAsync(configuration.UserEmail);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(configuration.UserPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Order history", Exact = true })).ToBeVisibleAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "REHEARSAL-LEGACY-001", Exact = true })).ToBeVisibleAsync();
    }
}
