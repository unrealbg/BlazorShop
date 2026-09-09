namespace BlazorShop.E2E;

using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

[Collection(E2ECollection.Name)]
public sealed class StorefrontCheckoutTests(E2EApplicationFixture application) : PageTest
{
    public override BrowserNewContextOptions ContextOptions()
    {
        return new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true,
            Locale = "en-US",
        };
    }

    [Fact]
    public async Task HomeCategoryAndProductNavigation_ShowsSeededCatalogContent()
    {
        await RunScenarioAsync(nameof(HomeCategoryAndProductNavigation_ShowsSeededCatalogContent), async data =>
        {
            await Page.GotoAsync(application.StorefrontBaseUrl.ToString());

            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Browse by Category" }))
                .ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Link, new() { Name = data.CategoryName }).First.ClickAsync();

            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = data.CategoryName, Exact = true }))
                .ToBeVisibleAsync();
            await Expect(Page.GetByText(data.SimpleProductDescription, new() { Exact = true }))
                .ToBeVisibleAsync();
            await Page.GetByRole(AriaRole.Link, new() { Name = data.SimpleProductName, Exact = true }).First.ClickAsync();

            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = data.SimpleProductName, Exact = true }))
                .ToBeVisibleAsync();
            await Expect(Page.GetByText(data.SimpleProductDescription, new() { Exact = true }))
                .ToBeVisibleAsync();
            await Expect(Page.GetByText($"€ {Money(data.SimpleProductPrice)}", new() { Exact = true }))
                .ToBeVisibleAsync();
        });
    }

    [Fact]
    public async Task Cart_AddsProductAndVariantAndPersistsQuantityAfterReload()
    {
        await RunScenarioAsync(nameof(Cart_AddsProductAndVariantAndPersistsQuantityAfterReload), async data =>
        {
            await AddProductToCartAsync(data.SimpleProductSlug, data.SimpleProductName);
            await Page.GetByRole(AriaRole.Link, new() { Name = "View Cart", Exact = true }).ClickAsync();

            var simpleLine = CartLine(data.SimpleProductName);
            await Expect(simpleLine).ToContainTextAsync("Quantity");
            await Expect(simpleLine.GetByRole(AriaRole.Spinbutton)).ToHaveValueAsync("1");
            await Expect(simpleLine).ToContainTextAsync($"EUR {Money(data.SimpleProductPrice)}");

            await Page.GotoAsync(ProductUrl(data.VariantProductSlug));
            var variantSelect = Page.GetByLabel("Choose a variant", new() { Exact = true });
            await variantSelect.SelectOptionAsync(data.VariantId.ToString("D", CultureInfo.InvariantCulture));
            await Expect(variantSelect).ToHaveValueAsync(data.VariantId.ToString("D", CultureInfo.InvariantCulture));
            await ProductPurchaseButton().ClickAsync();
            await Expect(ProductCartFeedback())
                .ToHaveTextAsync($"Product {data.VariantProductName} (size {data.VariantSize}) added to cart");
            await Page.GetByRole(AriaRole.Link, new() { Name = "View Cart", Exact = true }).ClickAsync();

            var variantLine = CartLine(data.VariantProductName);
            await Expect(variantLine).ToContainTextAsync($"SKU {data.VariantSku}");
            await Expect(variantLine).ToContainTextAsync($"Size {data.VariantSize}");
            await Expect(variantLine).ToContainTextAsync($"EUR {Money(data.VariantPrice)}");

            var variantQuantity = variantLine.GetByRole(AriaRole.Spinbutton);
            await variantQuantity.FillAsync("3");
            await variantQuantity.PressAsync("Tab");

            variantLine = CartLine(data.VariantProductName);
            await Expect(variantLine.GetByRole(AriaRole.Spinbutton)).ToHaveValueAsync("3");
            await Expect(variantLine).ToContainTextAsync($"EUR {Money(data.VariantPrice * 3)}");
            await Expect(Page.GetByText($"EUR {Money(data.SimpleProductPrice + (data.VariantPrice * 3))}", new() { Exact = true }).First)
                .ToBeVisibleAsync();

            await Page.ReloadAsync();

            variantLine = CartLine(data.VariantProductName);
            await Expect(variantLine.GetByRole(AriaRole.Spinbutton)).ToHaveValueAsync("3");
            await Expect(variantLine).ToContainTextAsync($"SKU {data.VariantSku}");
            await Expect(variantLine).ToContainTextAsync($"EUR {Money(data.VariantPrice * 3)}");
        });
    }

    [Fact]
    public async Task CheckoutStart_AnonymousLoginThenAuthenticatedCheckoutPreservesCart()
    {
        await RunScenarioAsync(nameof(CheckoutStart_AnonymousLoginThenAuthenticatedCheckoutPreservesCart), async data =>
        {
            await Page.GotoAsync(ProductUrl(data.VariantProductSlug));
            await Page.GetByLabel("Choose a variant", new() { Exact = true })
                .SelectOptionAsync(data.VariantId.ToString("D", CultureInfo.InvariantCulture));
            await ProductPurchaseButton().ClickAsync();
            await Expect(ProductCartFeedback())
                .ToHaveTextAsync($"Product {data.VariantProductName} (size {data.VariantSize}) added to cart");
            await Page.GetByRole(AriaRole.Link, new() { Name = "View Cart", Exact = true }).ClickAsync();

            var cartLine = CartLine(data.VariantProductName);
            var quantity = cartLine.GetByRole(AriaRole.Spinbutton);
            await quantity.FillAsync("2");
            await quantity.PressAsync("Tab");
            await Expect(CartLine(data.VariantProductName).GetByRole(AriaRole.Spinbutton)).ToHaveValueAsync("2");

            await Page.GetByRole(AriaRole.Link, new() { Name = "Continue to Checkout", Exact = true }).ClickAsync();

            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Sign in", Exact = true }))
                .ToBeVisibleAsync();
            await Expect(Page).ToHaveURLAsync(new Regex("/authentication/login/account/checkout$"));

            await Page.GetByLabel("Email Address", new() { Exact = true }).FillAsync(data.UserEmail);
            await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(data.UserPassword);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login", Exact = true }).ClickAsync();

            await Expect(Page).ToHaveURLAsync(new Regex("/account/checkout$"));
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My Cart", Exact = true }))
                .ToBeVisibleAsync();
            var checkoutRow = Page.GetByRole(AriaRole.Row).Filter(new() { HasText = data.VariantProductName });
            await Expect(checkoutRow).ToContainTextAsync($"SKU {data.VariantSku}");
            await Expect(checkoutRow).ToContainTextAsync($"Size {data.VariantSize}");
            await Expect(checkoutRow.GetByRole(AriaRole.Spinbutton)).ToHaveValueAsync("2");
            await Expect(checkoutRow).ToContainTextAsync($"€ {Money(data.VariantPrice * 2)}");
        });
    }

    private ILocator CartLine(string productName)
    {
        return Page.GetByRole(AriaRole.Article).Filter(new() { HasText = productName });
    }

    private async Task AddProductToCartAsync(string productSlug, string productName)
    {
        await Page.GotoAsync(ProductUrl(productSlug));
        await ProductPurchaseButton().ClickAsync();
        await Expect(ProductCartFeedback()).ToHaveTextAsync($"Product {productName} added to cart");
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
        return new Uri(application.StorefrontBaseUrl, $"product/{slug}").ToString();
    }

    private async Task RunScenarioAsync(string scenarioName, Func<E2ETestData, Task> scenario)
    {
        Page.SetDefaultTimeout(15_000);
        Page.SetDefaultNavigationTimeout(30_000);
        var data = await application.SeedScenarioAsync();
        await Context.Tracing.StartAsync(new()
        {
            Screenshots = true,
            Snapshots = true,
            Sources = true,
        });

        try
        {
            await scenario(data);
            await Context.Tracing.StopAsync();
        }
        catch
        {
            Directory.CreateDirectory(application.ArtifactDirectory);
            var artifactPrefix = Path.Combine(application.ArtifactDirectory, scenarioName);

            try
            {
                await Page.ScreenshotAsync(new()
                {
                    Path = $"{artifactPrefix}.png",
                    FullPage = true,
                });
                await Context.Tracing.StopAsync(new() { Path = $"{artifactPrefix}.zip" });
            }
            catch (PlaywrightException)
            {
                // Preserve the original scenario failure if the browser closed before diagnostics completed.
            }

            throw;
        }
        finally
        {
            await application.RemoveScenarioAsync(data);
        }
    }

    private static string Money(decimal amount)
    {
        return amount.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
