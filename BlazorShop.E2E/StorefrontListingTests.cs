namespace BlazorShop.E2E;

using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

[Collection(E2ECollection.Name)]
public sealed class StorefrontListingTests(E2EApplicationFixture application) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new()
    {
        IgnoreHTTPSErrors = true,
        Locale = "en-US",
    };

    [Fact]
    public async Task ListingPages_ResponsiveImagesNavigationAndVariantCommerce()
    {
        var data = await application.SeedScenarioAsync(includeListingSamples: true);
        var directory = Path.Combine(application.ArtifactDirectory, "ui-v2-listings");
        Directory.CreateDirectory(directory);
        try
        {
            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                await Page.SetViewportSizeAsync(width, 1000);
                foreach (var route in new[] { "new-releases", "todays-deals" })
                {
                    var response = await Page.GotoAsync(new Uri(application.StorefrontBaseUrl, route).ToString());
                    Assert.NotNull(response);
                    Assert.Equal(200, response.Status);
                    await Expect(Page.Locator("main h1")).ToHaveTextAsync(route == "new-releases" ? "Fresh Arrivals This Week" : "Today's Featured Picks");
                    var cards = Page.Locator(".bs-v2-product-grid > li");
                    var count = await cards.CountAsync();
                    Assert.InRange(count, 2, route == "new-releases" ? 24 : 12);
                    await Expect(Page.Locator(".bs-v2-listing .bs-v2-count")).ToHaveTextAsync($"{count} products");
                    await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = $"Unpublished {data.SimpleProductName}", Exact = true })).ToHaveCountAsync(0);
                    if (route == "new-releases")
                    {
                        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = $"Older {data.SimpleProductName}", Exact = true })).ToHaveCountAsync(0);
                    }

                    var simple = Page.GetByRole(AriaRole.Article).Filter(new()
                    {
                        Has = Page.GetByRole(AriaRole.Heading, new() { Name = data.SimpleProductName, Exact = true }),
                    });
                    var image = simple.GetByRole(AriaRole.Img, new() { Name = data.SimpleProductName, Exact = true });
                    await image.ScrollIntoViewIfNeededAsync();
                    await Expect(image).ToHaveJSPropertyAsync("complete", true);
                    Assert.True(await image.EvaluateAsync<bool>("img => img.naturalWidth > 0"));
                    var variant = Page.GetByRole(AriaRole.Article).Filter(new() { HasText = data.VariantProductName });
                    await Expect(variant.Locator(".bs-v2-image-placeholder")).ToHaveTextAsync("No image available");
                    await Expect(variant.Locator("[data-storefront-add-to-cart]")).ToHaveCountAsync(0);
                    await Expect(variant.GetByRole(AriaRole.Link, new() { Name = "Add to Cart", Exact = true }))
                        .ToHaveAttributeAsync("href", $"/product/{data.VariantProductSlug}#purchase");
                    await Expect(Page.Locator(".bs-v2-listing del, .bs-v2-listing s")).ToHaveCountAsync(0);

                    var links = Page.Locator(".bs-v2-listing-links a");
                    await links.First.FocusAsync();
                    await links.First.PressAsync("Tab");
                    await Expect(links.Nth(1)).ToBeFocusedAsync();
                    Assert.True(await links.Nth(1).EvaluateAsync<bool>("a => a.matches(':focus-visible') && parseFloat(getComputedStyle(a).outlineWidth) >= 2"));
                    foreach (var link in await links.AllAsync())
                    {
                        var bounds = await link.BoundingBoxAsync();
                        Assert.NotNull(bounds);
                        Assert.True(bounds.Height >= 44 && bounds.X >= 0 && bounds.X + bounds.Width <= width);
                    }

                    Assert.True(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
                    foreach (var lazyImage in await Page.Locator("main img[loading='lazy']").AllAsync())
                    {
                        await lazyImage.ScrollIntoViewIfNeededAsync();
                        await Expect(lazyImage).ToHaveJSPropertyAsync("complete", true);
                    }

                    await Page.EvaluateAsync("document.activeElement.blur(); scrollTo(0, 0)");
                    await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"{route}-{width}.png"), FullPage = true });
                    await simple.GetByRole(AriaRole.Link, new() { Name = "View Product", Exact = true }).ClickAsync();
                    await Expect(Page).ToHaveURLAsync(new Uri(application.StorefrontBaseUrl, $"product/{data.SimpleProductSlug}").ToString());
                    await Expect(Page.Locator("main h1")).ToHaveTextAsync(data.SimpleProductName);
                }
            }

            await Page.GotoAsync(new Uri(application.StorefrontBaseUrl, "new-releases").ToString());
            await Page.GetByRole(AriaRole.Article).Filter(new()
                {
                    Has = Page.GetByRole(AriaRole.Heading, new() { Name = data.SimpleProductName, Exact = true }),
                })
                .GetByRole(AriaRole.Button, new() { Name = "Add to Cart", Exact = true }).ClickAsync();
            await Expect(Page.Locator("[data-storefront-cart-badge]")).ToHaveTextAsync("1");
            await Page.ReloadAsync();
            await Expect(Page.Locator("[data-storefront-cart-badge]")).ToHaveTextAsync("1");
            await Page.Locator(".bs-v2-listing-links").GetByRole(AriaRole.Link, new() { Name = "See Today's Deals", Exact = true }).ClickAsync();
            await Expect(Page).ToHaveURLAsync(new Uri(application.StorefrontBaseUrl, "todays-deals").ToString());
            await Page.GetByRole(AriaRole.Article).Filter(new() { HasText = data.VariantProductName })
                .GetByRole(AriaRole.Link, new() { Name = "Add to Cart", Exact = true }).ClickAsync();
            await Expect(Page).ToHaveURLAsync(new Uri(application.StorefrontBaseUrl, $"product/{data.VariantProductSlug}#purchase").ToString());
            await Page.GetByLabel("Choose a variant", new() { Exact = true }).SelectOptionAsync(data.VariantId.ToString("D"));
            await Page.Locator("#purchase").GetByRole(AriaRole.Button, new() { Name = "Add to Cart", Exact = true }).ClickAsync();
            await Page.GetByRole(AriaRole.Link, new() { Name = "View Cart", Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Article).Filter(new() { HasText = data.VariantProductName })).ToContainTextAsync($"SKU {data.VariantSku}");
            await Page.ReloadAsync();
            await Expect(Page.GetByRole(AriaRole.Article).Filter(new() { HasText = data.SimpleProductName })).ToBeVisibleAsync();
            await Expect(Page.GetByRole(AriaRole.Article).Filter(new() { HasText = data.VariantProductName })).ToContainTextAsync($"Size {data.VariantSize}");

            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "BlazorShop.sln")))
            {
                root = root.Parent;
            }

            Assert.NotNull(root);
            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                await Page.SetViewportSizeAsync(width, 1000);
                foreach (var route in new[] { "new-releases", "todays-deals" })
                {
                    await Page.GotoAsync(new Uri(Path.Combine(root.FullName, "docs", "ui-v2", $"{route}.html")).AbsoluteUri);
                    await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"prototype-{route}-{width}.png"), FullPage = true });
                }
            }
        }
        finally
        {
            await application.RemoveScenarioAsync(data);
        }
    }
}
