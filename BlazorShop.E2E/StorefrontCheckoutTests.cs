namespace BlazorShop.E2E;

using System.Globalization;
using System.Text.Json;
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
            await AssertCartCookieLineAsync(data.VariantProductId, data.VariantId, 2);

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
            await AssertCartCookieLineAsync(data.VariantProductId, data.VariantId, 2);

            await Page.GotoAsync(application.StorefrontBaseUrl.ToString());
            var footer = Page.GetByRole(AriaRole.Contentinfo);
            await Expect(footer.GetByRole(AriaRole.Link, new() { Name = "My Account", Exact = true }))
                .ToHaveAttributeAsync("href", new Uri(application.WebBaseUrl, "account").ToString());
            await Expect(footer.GetByRole(AriaRole.Link, new() { Name = "Sign in", Exact = true })).ToHaveCountAsync(0);
            await Expect(footer.GetByRole(AriaRole.Link, new() { Name = "Register", Exact = true })).ToHaveCountAsync(0);
        });
    }

    [Fact]
    public async Task Cart_ResponsiveVariantsRemovalClearAndInvalidStates()
    {
        await RunScenarioAsync(nameof(Cart_ResponsiveVariantsRemovalClearAndInvalidStates), async data =>
        {
            var second = await application.SeedCartVariantAsync(data);
            var directory = Path.Combine(application.ArtifactDirectory, "ui-v2-cart");
            Directory.CreateDirectory(directory);
            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                await CaptureCartAsync(directory, "empty", width);
                await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Your cart is empty", Exact = true })).ToBeVisibleAsync();
                await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Continue to Checkout", Exact = true })).ToHaveCountAsync(0);
                await AssertCartTotalsAsync(0, 0m);
            }

            await AddProductToCartAsync(data.SimpleProductSlug, data.SimpleProductName);
            await Page.GotoAsync(ProductUrl(data.VariantProductSlug));
            await Page.GetByLabel("Choose a variant", new() { Exact = true }).SelectOptionAsync(data.VariantId.ToString("D"));
            await ProductPurchaseButton().ClickAsync();
            await Expect(ProductCartFeedback()).ToHaveTextAsync($"Product {data.VariantProductName} (size {data.VariantSize}) added to cart");
            await Page.GetByLabel("Choose a variant", new() { Exact = true }).SelectOptionAsync(second.Id.ToString("D"));
            await ProductPurchaseButton().ClickAsync();
            await Expect(ProductCartFeedback()).ToHaveTextAsync($"Product {data.VariantProductName} (size {second.Size}) added to cart");
            await Page.GetByRole(AriaRole.Link, new() { Name = "View Cart", Exact = true }).ClickAsync();

            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                await CaptureCartAsync(directory, "filled", width);
                await Expect(Page.Locator(".bs-v2-cart-line")).ToHaveCountAsync(3);
                await AssertCartTotalsAsync(3, data.SimpleProductPrice + data.VariantPrice + second.Price);
                var firstLine = CartVariantLine(data.VariantId);
                var secondLine = CartVariantLine(second.Id);
                await Expect(firstLine).ToContainTextAsync($"SKU {data.VariantSku}");
                await Expect(firstLine).ToContainTextAsync("Blue");
                await Expect(secondLine).ToContainTextAsync($"SKU {second.Sku}");
                await Expect(secondLine).ToContainTextAsync($"Size {second.Size}");
                await Expect(secondLine).ToContainTextAsync("Green");
                await Expect(firstLine.GetByRole(AriaRole.Button, new() { Name = "Remove", Exact = true }))
                    .ToHaveAccessibleDescriptionAsync(new Regex(Regex.Escape($"SKU {data.VariantSku}")));
                await Expect(secondLine.GetByRole(AriaRole.Button, new() { Name = "Remove", Exact = true }))
                    .ToHaveAccessibleDescriptionAsync(new Regex(Regex.Escape($"SKU {second.Sku}")));
                await Expect(firstLine.Locator(".bs-v2-image-placeholder")).ToHaveTextAsync("No image available");
                await Expect(CartLine(data.SimpleProductName).GetByRole(AriaRole.Link, new() { Name = data.SimpleProductName, Exact = true }))
                    .ToHaveAttributeAsync("href", $"/product/{data.SimpleProductSlug}");
                var quantity = firstLine.GetByRole(AriaRole.Spinbutton);
                await quantity.FocusAsync();
                await quantity.PressAsync("Tab");
                await Expect(firstLine.GetByRole(AriaRole.Button, new() { Name = "Remove", Exact = true })).ToBeFocusedAsync();
                await Page.Keyboard.PressAsync("Shift+Tab");
                await Expect(quantity).ToBeFocusedAsync();
                Assert.True(await quantity.EvaluateAsync<bool>("input => input.matches(':focus-visible') && parseFloat(getComputedStyle(input).outlineWidth) >= 2"));
                if (width == 320)
                {
                    await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "cart-keyboard-focus-320.png"), FullPage = true });
                }
            }

            var variantQuantity = CartVariantLine(data.VariantId).GetByRole(AriaRole.Spinbutton);
            await variantQuantity.FillAsync("3");
            await variantQuantity.PressAsync("Tab");
            await Expect(CartVariantLine(data.VariantId).GetByRole(AriaRole.Spinbutton)).ToHaveValueAsync("3");
            await AssertCartTotalsAsync(5, data.SimpleProductPrice + data.VariantPrice * 3 + second.Price);
            await Page.ReloadAsync();
            await Expect(CartVariantLine(data.VariantId).GetByRole(AriaRole.Spinbutton)).ToHaveValueAsync("3");
            await AssertCartCookieLineAsync(data.VariantProductId, data.VariantId, 3);
            await AssertCartCookieLineAsync(data.VariantProductId, second.Id, 1);
            await CartVariantLine(data.VariantId).GetByRole(AriaRole.Spinbutton).FillAsync("2");
            await CartVariantLine(data.VariantId).GetByRole(AriaRole.Spinbutton).PressAsync("Tab");
            await Expect(CartVariantLine(data.VariantId).GetByRole(AriaRole.Spinbutton)).ToHaveValueAsync("2");
            await AssertCartTotalsAsync(4, data.SimpleProductPrice + data.VariantPrice * 2 + second.Price);
            await CartVariantLine(data.VariantId).GetByRole(AriaRole.Button, new() { Name = "Remove", Exact = true }).ClickAsync();
            await Expect(CartVariantLine(data.VariantId)).ToHaveCountAsync(0);
            await Expect(CartVariantLine(second.Id)).ToHaveCountAsync(1);
            await Expect(Page.Locator("[data-storefront-toast-region]")).ToContainTextAsync($"Removed {data.VariantProductName} (size {data.VariantSize}) from cart.");
            await AssertCartTotalsAsync(2, data.SimpleProductPrice + second.Price);
            await AssertCartCookieLineAsync(data.VariantProductId, second.Id, 1);
            await Page.ReloadAsync();
            await Expect(CartVariantLine(second.Id)).ToHaveCountAsync(1);
            await CartLine(data.SimpleProductName).GetByRole(AriaRole.Button, new() { Name = "Remove", Exact = true }).ClickAsync();
            await Expect(CartLine(data.SimpleProductName)).ToHaveCountAsync(0);
            await AssertCartTotalsAsync(1, second.Price);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Clear Cart", Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Your cart is empty", Exact = true })).ToBeVisibleAsync();
            await Expect(Page.Locator("[data-storefront-toast-region]")).ToContainTextAsync("Cart cleared.");
            await AssertCartTotalsAsync(0, 0m);
            Assert.DoesNotContain(await Context.CookiesAsync(), cookie => cookie.Name == "my-cart");

            await SetCartCookieAsync("{broken-json");
            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                await CaptureCartAsync(directory, "invalid-cookie", width);
                await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("We couldn't read the saved cart cookie");
                await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Your cart is empty", Exact = true })).ToBeVisibleAsync();
            }

            await SetCartCookieAsync(JsonSerializer.Serialize(new[]
            {
                new { ProductId = Guid.NewGuid(), VariantId = (Guid?)null, Quantity = 1, UnitPrice = 999m },
                new { ProductId = data.VariantProductId, VariantId = (Guid?)Guid.NewGuid(), Quantity = 1, UnitPrice = 999m },
            }));
            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                await CaptureCartAsync(directory, "unavailable", width);
                await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("2 cart items could not be refreshed");
                await Expect(Page.Locator(".bs-v2-cart-unavailable")).ToHaveCountAsync(2);
                await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Unavailable item", Exact = true })).ToBeVisibleAsync();
                await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "Continue to Checkout", Exact = true })).ToBeVisibleAsync();
            }

            await Page.GetByRole(AriaRole.Button, new() { Name = "Clear Cart", Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Your cart is empty", Exact = true })).ToBeVisibleAsync();
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "BlazorShop.sln")))
            {
                root = root.Parent;
            }

            Assert.NotNull(root);
            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                await Page.SetViewportSizeAsync(width, 1000);
                await Page.GotoAsync(new Uri(Path.Combine(root.FullName, "docs", "ui-v2", "cart.html")).AbsoluteUri);
                await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"prototype-cart-{width}.png"), FullPage = true });
            }
        });
    }

    private ILocator CartVariantLine(Guid variantId)
    {
        return Page.Locator($".bs-v2-cart-line:has([data-storefront-cart-quantity][data-variant-id='{variantId:D}'])");
    }

    private async Task SetCartCookieAsync(string json)
    {
        await Context.AddCookiesAsync([new Cookie { Name = "my-cart", Value = Uri.EscapeDataString(json), Url = application.StorefrontBaseUrl.ToString() }]);
    }

    private async Task AssertCartTotalsAsync(int itemCount, decimal subtotal)
    {
        await Expect(Page.Locator(".bs-v2-cart-intro .bs-v2-count")).ToHaveTextAsync($"{itemCount} {(itemCount == 1 ? "item" : "items")} in cart");
        await Expect(Page.Locator(".bs-v2-cart-totals > div").Filter(new() { HasText = "Subtotal" }).Locator("dd"))
            .ToHaveTextAsync($"EUR {Money(subtotal)}");
        await Expect(Page.Locator("[data-storefront-cart-badge]")).ToHaveTextAsync(itemCount.ToString(CultureInfo.InvariantCulture));
        if (itemCount == 0)
        {
            await Expect(Page.Locator("[data-storefront-cart-badge]")).ToBeHiddenAsync();
        }
        else
        {
            await Expect(Page.Locator("[data-storefront-cart-badge]")).ToBeVisibleAsync();
        }
    }

    private async Task CaptureCartAsync(string directory, string state, int width)
    {
        await Page.SetViewportSizeAsync(width, 1000);
        var response = await Page.GotoAsync(new Uri(application.StorefrontBaseUrl, "my-cart").ToString());
        Assert.NotNull(response);
        Assert.Equal(200, response.Status);
        Assert.Contains("no-store", response.Headers["cache-control"], StringComparison.Ordinal);
        Assert.Contains("noindex", response.Headers["x-robots-tag"], StringComparison.Ordinal);
        Assert.True(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        foreach (var image in await Page.Locator(".bs-v2-cart-image img").AllAsync())
        {
            await Expect(image).ToHaveJSPropertyAsync("complete", true);
            Assert.True(await image.EvaluateAsync<bool>("img => img.naturalWidth > 0"));
        }

        foreach (var control in await Page.Locator(".bs-v2-cart :is(button,input,.bs-v2-button,.bs-v2-cart-product-link)").AllAsync())
        {
            var bounds = await control.BoundingBoxAsync();
            Assert.NotNull(bounds);
            Assert.True(bounds.Height >= 44 && bounds.X >= 0 && bounds.X + bounds.Width <= width);
        }

        var panel = await Page.Locator(".bs-v2-cart-panel").BoundingBoxAsync();
        var summary = await Page.Locator(".bs-v2-cart-summary").BoundingBoxAsync();
        Assert.NotNull(panel);
        Assert.NotNull(summary);
        Assert.True(width <= 980 ? summary.Y >= panel.Y + panel.Height : summary.X >= panel.X + panel.Width);
        await Page.EvaluateAsync("document.activeElement.blur(); scrollTo(0,0)");
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"cart-{state}-{width}.png"), FullPage = true });
    }

    private async Task AssertCartCookieLineAsync(Guid productId, Guid variantId, int quantity)
    {
        var cookie = Assert.Single(await Context.CookiesAsync(), candidate => candidate.Name == "my-cart");
        using var document = JsonDocument.Parse(Uri.UnescapeDataString(cookie.Value));
        var line = Assert.Single(document.RootElement.EnumerateArray(), candidate =>
            candidate.GetProperty("ProductId").GetGuid() == productId && candidate.GetProperty("VariantId").GetGuid() == variantId);
        Assert.Equal(quantity, line.GetProperty("Quantity").GetInt32());
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
