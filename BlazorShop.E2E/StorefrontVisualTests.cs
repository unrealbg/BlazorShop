namespace BlazorShop.E2E;

using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

[Collection(E2ECollection.Name)]
public sealed class StorefrontVisualTests(E2EApplicationFixture application) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        IgnoreHTTPSErrors = true,
        Locale = "en-US",
    };

    [Fact]
    public async Task StorefrontFoundation_ResponsiveNavigationAndCommerce()
    {
        var data = await application.SeedScenarioAsync();
        var directory = Path.Combine(application.ArtifactDirectory, "ui-v2");
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                await Page.SetViewportSizeAsync(width, 1000);
                await Page.GotoAsync(application.StorefrontBaseUrl.ToString());
                await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Discover great products.", Exact = true }))
                    .ToBeVisibleAsync();
                var hero = Page.Locator(".bs-v2-hero-actions");
                await hero.GetByRole(AriaRole.Link, new() { Name = "Shop now", Exact = true }).ClickAsync();
                await Expect(Page).ToHaveURLAsync(new Uri(application.StorefrontBaseUrl, "#latest").ToString());
                await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Latest Products", Exact = true })).ToBeInViewportAsync();
                await hero.GetByRole(AriaRole.Link, new() { Name = "New arrivals", Exact = true }).ClickAsync();
                await Expect(Page).ToHaveURLAsync(new Uri(application.StorefrontBaseUrl, "new-releases").ToString());
                await Page.GotoAsync(application.StorefrontBaseUrl.ToString());
                Assert.True(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
                var art = Page.GetByRole(AriaRole.Img, new() { Name = "Modern armchair and side table", Exact = true });
                Assert.True(await art.EvaluateAsync<bool>("img => img.complete && img.naturalWidth > 0"));
                var skip = Page.GetByRole(AriaRole.Link, new() { Name = "Skip to content", Exact = true });
                await skip.FocusAsync();
                await skip.PressAsync("Enter");
                await Expect(Page.Locator("#main-content")).ToBeFocusedAsync();

                var account = Page.GetByLabel("Account menu", new() { Exact = true });
                await account.FocusAsync();
                await account.PressAsync("Enter");
                await Expect(Page.Locator(".bs-storefront-account-menu").GetByRole(AriaRole.Link, new() { Name = "Sign in", Exact = true }))
                    .ToHaveAttributeAsync("href", "/signin");
                await account.PressAsync("Enter");

                var mobileMenu = Page.Locator(".bs-v2-mobile-nav > summary");
                var navigation = Page.GetByRole(AriaRole.Navigation, new() { Name = width <= 980 ? "Mobile shop navigation" : "Primary shop navigation", Exact = true });
                if (width <= 980)
                {
                    await mobileMenu.FocusAsync();
                    await mobileMenu.PressAsync("Enter");
                }

                await navigation.GetByRole(AriaRole.Link, new() { Name = "New Releases", Exact = true }).ClickAsync();
                await Expect(Page).ToHaveURLAsync(new Uri(application.StorefrontBaseUrl, "new-releases").ToString());
                await Page.GotoAsync(application.StorefrontBaseUrl.ToString());
                await CapturePageAsync(Path.Combine(directory, $"home-{width}.png"));
                await Page.GotoAsync(new Uri(application.StorefrontBaseUrl, $"category/{data.CategorySlug}").ToString());
                await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = data.CategoryName, Exact = true }))
                    .ToBeVisibleAsync();
                Assert.True(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
                await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = data.SimpleProductName, Exact = true }))
                    .ToBeVisibleAsync();
                foreach (var button in await Page.Locator(".bs-v2-product-actions > *").AllAsync())
                {
                    var bounds = await button.BoundingBoxAsync();
                    Assert.NotNull(bounds);
                    Assert.True(bounds.Height >= 44 && bounds.X >= 0 && bounds.X + bounds.Width <= width);
                }

                await CapturePageAsync(Path.Combine(directory, $"category-{width}.png"));
            }

            await Page.GotoAsync(application.StorefrontBaseUrl.ToString());
            var simpleCard = Page.GetByRole(AriaRole.Article).Filter(new() { HasText = data.SimpleProductName });
            await simpleCard.GetByRole(AriaRole.Button, new() { Name = "Add to Cart", Exact = true }).ClickAsync();
            await Expect(Page.Locator("[data-storefront-cart-badge]")).ToHaveTextAsync("1");
            await Page.ReloadAsync();
            await Expect(Page.Locator("[data-storefront-cart-badge]")).ToHaveTextAsync("1");
            var variantCard = Page.GetByRole(AriaRole.Article).Filter(new() { HasText = data.VariantProductName });
            await variantCard.GetByRole(AriaRole.Link, new() { Name = "Add to Cart", Exact = true }).ClickAsync();
            await Expect(Page).ToHaveURLAsync(new Uri(application.StorefrontBaseUrl, $"product/{data.VariantProductSlug}#purchase").ToString());
            await Expect(Page.GetByLabel("Choose a variant", new() { Exact = true })).ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Link, new() { Name = "Open cart", Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Article).Filter(new() { HasText = data.SimpleProductName }))
                .ToContainTextAsync(data.SimpleProductName);

            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "BlazorShop.sln")))
            {
                root = root.Parent;
            }

            Assert.NotNull(root);
            foreach (var width in new[] { 390, 768, 1440 })
            {
                await Page.SetViewportSizeAsync(width, 1000);
                await Page.GotoAsync(new Uri(Path.Combine(root.FullName, "docs", "ui-v2", "index.html")).AbsoluteUri);
                await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"prototype-{width}.png"), FullPage = true });
            }
        }
        finally
        {
            await application.RemoveScenarioAsync(data);
        }
    }

    private async Task CapturePageAsync(string path)
    {
        // Full-page screenshots alone do not load images below the viewport.
        foreach (var image in await Page.Locator("main img[loading='lazy']").AllAsync())
        {
            await image.ScrollIntoViewIfNeededAsync();
            await Expect(image).ToHaveJSPropertyAsync("complete", true);
        }

        await Page.EvaluateAsync("scrollTo(0, 0)");
        await Page.ScreenshotAsync(new() { Path = path, FullPage = true });
    }
}
