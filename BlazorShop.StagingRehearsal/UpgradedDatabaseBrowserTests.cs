namespace BlazorShop.StagingRehearsal;

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

public sealed class UpgradedDatabaseBrowserTests : PageTest
{
    private readonly RehearsalConfiguration configuration = RehearsalConfiguration.Load();

    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            Locale = "en-US",
        };
    }

    [Fact]
    [Trait("Category", "StagingRehearsal")]
    public async Task UpgradedHistoricalDatabase_CheckoutStartBrowserFlow()
    {
        Page.SetDefaultTimeout(15_000);
        Page.SetDefaultNavigationTimeout(30_000);
        Directory.CreateDirectory(configuration.ArtifactDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(configuration.ArtifactDirectory, "browser-version.txt"),
            $"playwright=1.61.0{Environment.NewLine}browser=Chromium {Browser.Version}{Environment.NewLine}");
        var tracePath = Path.Combine(configuration.ArtifactDirectory, "upgraded-database-pre-auth-trace.zip");

        await VerifyTargetProxyReadsKnownDatabaseMarkersAsync();
        await Context.Tracing.StartAsync(new()
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true,
        });

        try
        {
            await Page.GotoAsync(configuration.StorefrontBaseUrl.ToString());
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Browse by Category" }))
                .ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Link, new() { Name = configuration.CategoryName }).First.ClickAsync();

            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = configuration.CategoryName, Exact = true }))
                .ToBeVisibleAsync();
            await Expect(Page.GetByText(configuration.SimpleProductDescription, new() { Exact = true }))
                .ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Link, new() { Name = configuration.SimpleProductName, Exact = true }).First.ClickAsync();

            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = configuration.SimpleProductName, Exact = true }))
                .ToBeVisibleAsync();
            await ProductPurchaseButton().ClickAsync();
            await Expect(ProductCartFeedback()).ToHaveTextAsync($"Product {configuration.SimpleProductName} added to cart");
            await Page.GetByRole(AriaRole.Link, new() { Name = "View Cart", Exact = true }).ClickAsync();

            var simpleLine = CartLine(configuration.SimpleProductName);
            await Expect(simpleLine.GetByRole(AriaRole.Spinbutton)).ToHaveValueAsync("1");
            await Expect(simpleLine).ToContainTextAsync($"EUR {Money(configuration.SimpleProductPrice)}");

            await Page.GotoAsync(ProductUrl(configuration.VariantProductSlug));
            var variantSelect = Page.GetByLabel("Choose a variant", new() { Exact = true });
            await variantSelect.SelectOptionAsync(configuration.VariantId.ToString("D", CultureInfo.InvariantCulture));
            await Expect(variantSelect).ToHaveValueAsync(configuration.VariantId.ToString("D", CultureInfo.InvariantCulture));
            await ProductPurchaseButton().ClickAsync();
            await Expect(ProductCartFeedback())
                .ToHaveTextAsync($"Product {configuration.VariantProductName} (size {configuration.VariantSize}) added to cart");
            await Page.GetByRole(AriaRole.Link, new() { Name = "View Cart", Exact = true }).ClickAsync();

            var variantLine = CartLine(configuration.VariantProductName);
            await Expect(variantLine).ToContainTextAsync($"SKU {configuration.VariantSku}");
            await Expect(variantLine).ToContainTextAsync($"Size {configuration.VariantSize}");
            await Expect(variantLine).ToContainTextAsync($"EUR {Money(configuration.VariantPrice)}");

            var quantity = variantLine.GetByRole(AriaRole.Spinbutton);
            await quantity.FillAsync("3");
            await quantity.PressAsync("Tab");
            await Expect(CartLine(configuration.VariantProductName).GetByRole(AriaRole.Spinbutton)).ToHaveValueAsync("3");
            await Expect(CartLine(configuration.VariantProductName))
                .ToContainTextAsync($"EUR {Money(configuration.VariantPrice * 3)}");

            await Page.ReloadAsync();
            variantLine = CartLine(configuration.VariantProductName);
            await Expect(variantLine.GetByRole(AriaRole.Spinbutton)).ToHaveValueAsync("3");
            await Expect(variantLine).ToContainTextAsync($"SKU {configuration.VariantSku}");

            await Page.GetByRole(AriaRole.Link, new() { Name = "Continue to Checkout", Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Sign in", Exact = true }))
                .ToBeVisibleAsync();
            await Expect(Page).ToHaveURLAsync(new Regex("/authentication/login/account/checkout$"));

            // Keep credentials and authenticated browser state out of uploaded Playwright traces.
            await Context.Tracing.StopAsync(new() { Path = tracePath });
            await Page.GetByLabel("Email Address", new() { Exact = true }).FillAsync(configuration.UserEmail);
            await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(configuration.UserPassword);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login", Exact = true }).ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/account/checkout$"));
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My Cart", Exact = true }))
                .ToBeVisibleAsync();

            var simpleCheckoutRow = Page.GetByRole(AriaRole.Row).Filter(new() { HasText = configuration.SimpleProductName });
            await Expect(simpleCheckoutRow.GetByRole(AriaRole.Spinbutton)).ToHaveValueAsync("1");
            await Expect(simpleCheckoutRow).ToContainTextAsync($"€ {Money(configuration.SimpleProductPrice)}");

            var variantCheckoutRow = Page.GetByRole(AriaRole.Row).Filter(new() { HasText = configuration.VariantProductName });
            await Expect(variantCheckoutRow).ToContainTextAsync($"SKU {configuration.VariantSku}");
            await Expect(variantCheckoutRow).ToContainTextAsync($"Size {configuration.VariantSize}");
            await Expect(variantCheckoutRow.GetByRole(AriaRole.Spinbutton)).ToHaveValueAsync("3");
            await Expect(variantCheckoutRow).ToContainTextAsync($"€ {Money(configuration.VariantPrice * 3)}");

            File.Delete(tracePath);
        }
        catch
        {
            await RedactPasswordInputAsync();
            await Page.ScreenshotAsync(new()
            {
                Path = Path.Combine(configuration.ArtifactDirectory, "upgraded-database-failure.png"),
                FullPage = true,
            });

            try
            {
                await Context.Tracing.StopAsync(new() { Path = tracePath });
            }
            catch (PlaywrightException)
            {
                // The pre-auth trace is already saved or the browser closed; preserve the scenario failure.
            }

            throw;
        }
    }

    private async Task VerifyTargetProxyReadsKnownDatabaseMarkersAsync()
    {
        using var client = new HttpClient { BaseAddress = configuration.WebBaseUrl };
        using var categoryResponse = await client.GetAsync($"api/category/single/{configuration.CategoryId:D}");
        categoryResponse.EnsureSuccessStatusCode();
        using var productResponse = await client.GetAsync($"api/product/single/{configuration.VariantProductId:D}");
        productResponse.EnsureSuccessStatusCode();

        using var categoryDocument = JsonDocument.Parse(await categoryResponse.Content.ReadAsStringAsync());
        using var productDocument = JsonDocument.Parse(await productResponse.Content.ReadAsStringAsync());
        Assert.Equal(configuration.CategoryId, categoryDocument.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(configuration.CategoryName, categoryDocument.RootElement.GetProperty("name").GetString());
        Assert.Equal(configuration.VariantProductId, productDocument.RootElement.GetProperty("id").GetGuid());
        Assert.Equal(configuration.VariantProductName, productDocument.RootElement.GetProperty("name").GetString());
    }

    private async Task RedactPasswordInputAsync()
    {
        var password = Page.GetByLabel("Password", new() { Exact = true });
        if (await password.CountAsync() > 0)
        {
            await password.FillAsync(string.Empty);
        }
    }

    private ILocator CartLine(string productName)
    {
        return Page.GetByRole(AriaRole.Article).Filter(new() { HasText = productName });
    }

    private ILocator ProductPurchaseButton()
    {
        return Page.Locator("#purchase")
            .GetByRole(AriaRole.Button, new() { Name = "Add to Cart", Exact = true });
    }

    private ILocator ProductCartFeedback()
    {
        return Page.Locator("#product-cart-feedback");
    }

    private string ProductUrl(string slug)
    {
        return new Uri(configuration.StorefrontBaseUrl, $"product/{slug}").ToString();
    }

    private static string Money(decimal amount)
    {
        return amount.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
