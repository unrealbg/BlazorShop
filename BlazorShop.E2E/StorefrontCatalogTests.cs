namespace BlazorShop.E2E;

using System.Text.RegularExpressions;

using BlazorShop.Domain.Entities;

using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

[Collection(E2ECollection.Name)]
public sealed class StorefrontCatalogTests(E2EApplicationFixture application) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new() { IgnoreHTTPSErrors = true, Locale = "en-US" };

    [Fact]
    public async Task CatalogSearch_NativeGetResponsiveImagesKeyboardAndSeoWithoutJavaScript()
    {
        var data = await application.SeedScenarioAsync(includeListingSamples: true);
        var directory = Path.Combine(application.ArtifactDirectory, "ui-v2-catalog-search");
        Directory.CreateDirectory(directory);
        await using var context = await Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, JavaScriptEnabled = false, Locale = "en-US" });
        var page = await context.NewPageAsync();
        try
        {
            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                await page.SetViewportSizeAsync(width, 1000);
                var response = await page.GotoAsync(CatalogUrl($"?category={data.CategoryId}"));
                Assert.NotNull(response);
                Assert.Equal(200, response.Status);
                Assert.Equal("noindex, follow", response.Headers["x-robots-tag"]);
                if (width < 981)
                {
                    await page.Locator(".bs-v2-mobile-nav > summary").ClickAsync();
                    await page.Locator(".bs-v2-mobile-nav nav").GetByRole(AriaRole.Link, new() { Name = "Catalog", Exact = true }).ClickAsync();
                }
                else
                {
                    await page.GetByRole(AriaRole.Navigation, new() { Name = "Primary shop navigation" }).GetByRole(AriaRole.Link, new() { Name = "Catalog", Exact = true }).ClickAsync();
                }

                await Expect(page).ToHaveURLAsync(CatalogUrl());
                await page.GetByRole(AriaRole.Navigation, new() { Name = "Footer shop navigation" }).GetByRole(AriaRole.Link, new() { Name = "Catalog", Exact = true }).ClickAsync();
                await page.Locator("#catalog-category").SelectOptionAsync(data.CategoryId.ToString());
                await page.GetByRole(AriaRole.Button, new() { Name = "Apply filters", Exact = true }).ClickAsync();
                await Expect(page.Locator("main h1")).ToHaveTextAsync("Shop all products");
                var filters = page.GetByRole(AriaRole.Form, new() { Name = "Catalog filters" });
                await Expect(filters.GetByLabel("Category", new() { Exact = true })).ToHaveValueAsync(data.CategoryId.ToString());
                await Expect(page.Locator(".bs-v2-catalog-results .bs-v2-count")).ToHaveTextAsync("3 products");
                await Expect(page.GetByRole(AriaRole.Heading, new() { Name = $"Unpublished {data.SimpleProductName}", Exact = true })).ToHaveCountAsync(0);
                var simple = page.GetByRole(AriaRole.Article).Filter(new() { Has = page.GetByRole(AriaRole.Heading, new() { Name = data.SimpleProductName, Exact = true }) });
                var image = simple.GetByRole(AriaRole.Img, new() { Name = data.SimpleProductName, Exact = true });
                await image.ScrollIntoViewIfNeededAsync();
                await Expect(image).ToHaveJSPropertyAsync("complete", true);
                Assert.True(await image.EvaluateAsync<bool>("img => img.naturalWidth > 0"));
                await Expect(page.GetByRole(AriaRole.Article).Filter(new() { HasText = data.VariantProductName }).Locator(".bs-v2-image-placeholder"))
                    .ToHaveTextAsync("No image available");
                await filters.GetByLabel("Search products", new() { Exact = true }).FocusAsync();
                await page.Keyboard.PressAsync("Tab");
                await Expect(filters.GetByLabel("Category", new() { Exact = true })).ToBeFocusedAsync();
                Assert.True(await filters.GetByLabel("Category", new() { Exact = true }).EvaluateAsync<bool>("e => e.matches(':focus-visible') && parseFloat(getComputedStyle(e).outlineWidth) >= 2"));
                foreach (var control in await filters.Locator("input,select,button,a").AllAsync())
                {
                    var bounds = await control.BoundingBoxAsync();
                    Assert.NotNull(bounds);
                    Assert.True(bounds.Height >= 44 && bounds.Width >= 44 && bounds.X >= 0 && bounds.X + bounds.Width <= width);
                }

                Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
                await CaptureAsync(page, Path.Combine(directory, $"catalog-{width}.png"));
                await filters.GetByLabel("Search products", new() { Exact = true }).FillAsync(data.SimpleProductName);
                await filters.GetByLabel("Sort products", new() { Exact = true }).SelectOptionAsync("price-low-to-high");
                await filters.GetByRole(AriaRole.Button, new() { Name = "Apply filters", Exact = true }).ClickAsync();
                await Expect(page.Locator("main h1")).ToHaveTextAsync("Search results");
                await Expect(page.Locator(".bs-v2-catalog-results .bs-v2-count")).ToHaveTextAsync("2 products");
                await Expect(page.Locator("#catalog-query")).ToHaveValueAsync(data.SimpleProductName);
                await Expect(page.Locator("#catalog-category")).ToHaveValueAsync(data.CategoryId.ToString());
                await Expect(page.Locator("#catalog-sort")).ToHaveValueAsync("price-low-to-high");
                Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
                await CaptureAsync(page, Path.Combine(directory, $"search-results-{width}.png"));
                await page.Locator("#catalog-query").FillAsync($"No match & 雨 {Guid.NewGuid():N}");
                await page.GetByRole(AriaRole.Button, new() { Name = "Apply filters", Exact = true }).ClickAsync();
                await Expect(page.Locator(".bs-v2-catalog-results .bs-v2-count")).ToHaveTextAsync("0 products");
                await Expect(page.GetByText("No products match your search or filters. Try a different term or reset the filters.", new() { Exact = true })).ToBeVisibleAsync();
                await CaptureAsync(page, Path.Combine(directory, $"empty-{width}.png"));
                await page.GetByRole(AriaRole.Link, new() { Name = "Reset filters", Exact = true }).ClickAsync();
                await Expect(page).ToHaveURLAsync(CatalogUrl());
                await page.GetByRole(AriaRole.Searchbox, new() { Name = "Search the shop", Exact = true }).FillAsync(data.VariantProductName);
                await page.GetByRole(AriaRole.Search, new() { Name = "Shop search" }).GetByRole(AriaRole.Button, new() { Name = "Search", Exact = true }).ClickAsync();
                await Expect(page.Locator(".bs-v2-catalog-results .bs-v2-count")).ToHaveTextAsync("1 product");
                await Expect(page.Locator("meta[name='robots']")).ToHaveAttributeAsync("content", "noindex,follow");
            }

            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "BlazorShop.sln"))) root = root.Parent;
            Assert.NotNull(root);
            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                await page.SetViewportSizeAsync(width, 1000);
                foreach (var name in new[] { "catalog", "search-results" })
                {
                    await page.GotoAsync(new Uri(Path.Combine(root.FullName, "docs", "ui-v2", $"{name}.html")).AbsoluteUri);
                    await page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"prototype-{name}-{width}.png"), FullPage = true });
                }
            }
        }
        finally
        {
            await application.RemoveScenarioAsync(data);
        }
    }

    [Fact]
    public async Task CatalogSearch_PaginationLegacySearchAndCommerceHandoffs()
    {
        var data = await application.SeedScenarioAsync(includeListingSamples: true);
        var search = $"Blue & red / 雨 + ?# {data.CategoryId:N}";
        var directory = Path.Combine(application.ArtifactDirectory, "ui-v2-catalog-search");
        Directory.CreateDirectory(directory);
        try
        {
            await application.WithAdminDatabaseAsync(async database =>
            {
                database.Products.AddRange(Enumerable.Range(1, 26).Select(index => new Product
                {
                    Id = Guid.NewGuid(), Name = $"Catalog {index:D2}", Slug = $"catalog-{data.CategoryId:N}-{index}",
                    Description = search, Price = index, Quantity = 10, IsPublished = true,
                    PublishedOn = DateTime.UtcNow.AddMinutes(-1), CategoryId = data.CategoryId,
                }));
                await database.SaveChangesAsync();
            });
            await Page.GotoAsync(CatalogUrl($"?q={Uri.EscapeDataString(search)}&category={data.CategoryId}&sort=price-low-to-high"));
            await Expect(Page.Locator(".bs-v2-catalog-results .bs-v2-count")).ToHaveTextAsync("26 products");
            await Expect(Page.Locator(".bs-v2-product-grid > li")).ToHaveCountAsync(24);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Catalog 01", Exact = true })).ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Link, new() { Name = "Next page", Exact = true }).ClickAsync();
            await Expect(Page.Locator(".bs-v2-product-grid > li")).ToHaveCountAsync(2);
            await Expect(Page.Locator("#catalog-query")).ToHaveValueAsync(search);
            await Expect(Page.Locator("link[rel='canonical']")).ToHaveAttributeAsync("href", Page.Url);
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Next page", Exact = true })).ToHaveCountAsync(0);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Catalog 25", Exact = true })).ToBeVisibleAsync();
            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                await Page.SetViewportSizeAsync(width, 1000);
                Assert.True(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
                foreach (var control in await Page.Locator(".bs-v2-catalog-pagination a").AllAsync())
                {
                    var bounds = await control.BoundingBoxAsync();
                    Assert.NotNull(bounds);
                    Assert.True(bounds.Height >= 44 && bounds.Width >= 44);
                }

                await CaptureAsync(Page, Path.Combine(directory, $"last-page-{width}.png"));
            }

            await Page.Locator("#catalog-sort").SelectOptionAsync("name-descending");
            await Page.Locator("#catalog-category").SelectOptionAsync(string.Empty);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Apply filters", Exact = true }).ClickAsync();
            Assert.DoesNotContain("page=", Page.Url, StringComparison.Ordinal);
            await Expect(Page.Locator("#catalog-category")).ToHaveValueAsync(string.Empty);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Catalog 26", Exact = true })).ToBeVisibleAsync();
            var missing = await Page.GotoAsync(CatalogUrl($"?q={Uri.EscapeDataString(search)}&category={data.CategoryId}&page=3"));
            Assert.NotNull(missing);
            Assert.Equal(404, missing.Status);
            await Expect(Page.Locator("link[rel='canonical']")).ToHaveCountAsync(0);

            // Keep the legacy WebAssembly route and its controls operational.
            var legacy = new Uri(application.WebBaseUrl, $"search-result/{Uri.EscapeDataString(data.SimpleProductName)}?sort=PriceHighToLow&category={data.CategoryId}").ToString();
            await Page.GotoAsync(legacy);
            await Expect(Page.Locator("#catalog-search")).ToHaveValueAsync(data.SimpleProductName);
            await Expect(Page.Locator("#catalog-sort")).ToHaveValueAsync("PriceHighToLow");
            await Expect(Page.Locator("#catalog-category")).ToHaveValueAsync(data.CategoryId.ToString());
            Assert.Equal(new Uri(legacy), new Uri(Page.Url));

            await Page.GotoAsync(CatalogUrl($"?q={Uri.EscapeDataString(data.SimpleProductName)}&category={data.CategoryId}"));
            await Page.GetByRole(AriaRole.Article).Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = data.SimpleProductName, Exact = true }) })
                .GetByRole(AriaRole.Button, new() { Name = "Add to Cart", Exact = true }).ClickAsync();
            await Expect(Page.Locator("[data-storefront-cart-badge]")).ToHaveTextAsync("1");
            await Page.GotoAsync(CatalogUrl($"?q={Uri.EscapeDataString(data.VariantProductName)}&category={data.CategoryId}"));
            await Page.GetByRole(AriaRole.Article).Filter(new() { HasText = data.VariantProductName })
                .GetByRole(AriaRole.Link, new() { Name = "Add to Cart", Exact = true }).ClickAsync();
            await Page.GetByLabel("Choose a variant", new() { Exact = true }).SelectOptionAsync(data.VariantId.ToString());
            await Page.Locator("#purchase").GetByRole(AriaRole.Button, new() { Name = "Add to Cart", Exact = true }).ClickAsync();
            await Page.GetByRole(AriaRole.Link, new() { Name = "View Cart", Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Article).Filter(new() { HasText = data.VariantProductName })).ToContainTextAsync($"SKU {data.VariantSku}");
            await Page.GetByRole(AriaRole.Link, new() { Name = "Continue to Checkout", Exact = true }).ClickAsync();
            await Expect(Page).ToHaveURLAsync(new Regex("/authentication/login/account/checkout$"));
            await Page.GetByLabel("Email Address", new() { Exact = true }).FillAsync(data.UserEmail);
            await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(data.UserPassword);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login", Exact = true }).ClickAsync();
            await Expect(Page).ToHaveURLAsync(new Regex("/account/checkout$"));
            await Page.GotoAsync(CatalogUrl());
            await Page.Locator(".bs-v2-header-actions").GetByRole(AriaRole.Link, new() { Name = "Open cart", Exact = true }).ClickAsync();
            await Page.GetByRole(AriaRole.Link, new() { Name = "Continue to Checkout", Exact = true }).ClickAsync();
            await Expect(Page).ToHaveURLAsync(new Regex("/account/checkout$"));
        }
        finally
        {
            await application.RemoveScenarioAsync(data);
        }
    }

    private string CatalogUrl(string query = "") => new Uri(application.StorefrontBaseUrl, "catalog" + query).ToString();

    private async Task CaptureAsync(IPage page, string path)
    {
        foreach (var image in await page.Locator("main img[loading='lazy']").AllAsync())
        {
            await image.ScrollIntoViewIfNeededAsync();
            await Expect(image).ToHaveJSPropertyAsync("complete", true);
        }

        await page.EvaluateAsync("document.activeElement.blur(); scrollTo(0, 0)");
        await page.ScreenshotAsync(new() { Path = path, FullPage = true });
    }
}
