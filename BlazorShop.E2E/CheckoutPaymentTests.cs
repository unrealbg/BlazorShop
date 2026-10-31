namespace BlazorShop.E2E;

using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

[Collection(E2ECollection.Name)]
public sealed partial class CheckoutPaymentTests(E2EApplicationFixture application) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new() { IgnoreHTTPSErrors = true, Locale = "en-US" };

    [Fact]
    public async Task CheckoutAndPublicPages_ResponsivePresentation()
    {
        var data = await application.SeedScenarioAsync(includeListingSamples: true);
        try
        {
            await Page.GotoAsync(new Uri(application.WebBaseUrl, "authentication/login/account/checkout").ToString());
            await Page.GetByLabel("Email Address", new() { Exact = true }).FillAsync(data.UserEmail);
            await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(data.UserPassword);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login", Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My Cart", Exact = true })).ToBeVisibleAsync();
            await Context.AddCookiesAsync([new Cookie
            {
                Name = "my-cart", Url = application.WebBaseUrl.ToString(),
                Value = System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new { ProductId = data.SimpleProductId, VariantId = (Guid?)null, Quantity = 1 },
                    new { ProductId = data.VariantProductId, VariantId = (Guid?)data.VariantId, Quantity = 2 },
                }),
            }]);
            await Page.ReloadAsync();
            await Expect(Page.GetByRole(AriaRole.Row).Filter(new() { HasText = data.VariantProductName })).ToBeVisibleAsync();
            await CaptureAllAsync("checkout");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Checkout", Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Select Payment Method", Exact = true })).ToBeVisibleAsync();
            await CaptureAllAsync("payment-methods");
            foreach (var (path, name) in new[]
            {
                ("payment-success?pm=cod", "result-cod"), ("payment-success?pm=bank", "result-bank"),
                ("payment-success?pm=card", "result-stripe"), ("payment-cancel", "result-cancel"),
            })
            {
                await Page.GotoAsync(new Uri(application.WebBaseUrl, path).ToString());
                await Expect(Page.GetByRole(AriaRole.Heading).First).ToBeVisibleAsync();
                await CaptureAllAsync(name);
            }
            foreach (var path in new[] { "about-us", "customer-service", "faq", "privacy", "terms", "unknown-public-page" })
            {
                await Page.GotoAsync(new Uri(application.StorefrontBaseUrl, path).ToString());
                await Expect(Page.Locator("h1")).ToBeVisibleAsync();
                await CaptureAllAsync(path);
            }
        }
        finally { await application.RemoveScenarioAsync(data); }
    }

    private async Task CaptureAllAsync(string name)
    {
        var directory = Path.Combine(application.ArtifactDirectory, "ui-v2-checkout-public");
        Directory.CreateDirectory(directory);
        foreach (var width in new[] { 320, 390, 768, 1440 })
        {
            await Page.SetViewportSizeAsync(width, 1000);
            await Page.EvaluateAsync("document.fonts.ready");
            Assert.True(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"), $"Page overflow: {name} at {width}px");
            Assert.True(await Page.Locator(".v2-checkout-panel, .v2-checkout-lines, .bs-v2-info-panel, dialog[open]").EvaluateAllAsync<bool>("elements => elements.every(e => { const r = e.getBoundingClientRect(); return r.left >= -1 && r.right <= innerWidth + 1 && e.scrollWidth <= e.clientWidth + 1; })"), $"Clipped content: {name} at {width}px");
            await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"{name}-{width}.png"), FullPage = true });
        }
    }
}
