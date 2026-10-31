namespace BlazorShop.E2E;

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

using BlazorShop.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

public sealed partial class CheckoutPaymentTests
{
    [Fact]
    public async Task CheckoutMethods_RealCodBankTransferAndDoubleSubmitGuard()
    {
        foreach (var method in new[] { "Cash on Delivery", "Bank Transfer" })
        {
            var data = await application.SeedScenarioAsync(includeListingSamples: true);
            try
            {
                await LoginCheckoutAsync(data);
                await SetCartAsync(data);
                await OpenPaymentAsync();
                await using var methodResponse = await Context.APIRequest.GetAsync(new Uri(application.ApiBaseUrl, "api/payment/methods").ToString());
                Assert.Equal(200, methodResponse.Status);
                using var methods = JsonDocument.Parse(await methodResponse.TextAsync());
                foreach (var item in methods.RootElement.EnumerateArray())
                    await Expect(Page.GetByRole(AriaRole.Listitem).Filter(new() { HasText = item.GetProperty("name").GetString()! })).ToBeVisibleAsync();

                var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var submissions = 0;
                JsonElement payload = default;
                await Page.RouteAsync("**/api/cart/checkout", async route =>
                {
                    Interlocked.Increment(ref submissions);
                    Assert.True(Guid.TryParse(route.Request.Headers["idempotency-key"], out _));
                    using var request = JsonDocument.Parse(route.Request.PostData!);
                    var line = Assert.Single(request.RootElement.GetProperty("carts").EnumerateArray());
                    Assert.Equal(data.VariantProductId, line.GetProperty("productId").GetGuid());
                    Assert.Equal(data.VariantId, line.GetProperty("variantId").GetGuid());
                    Assert.Equal(2, line.GetProperty("quantity").GetInt32());
                    started.TrySetResult();
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    var response = await route.FetchAsync();
                    Assert.Equal(200, response.Status);
                    using var body = JsonDocument.Parse(await response.TextAsync());
                    Assert.True(body.RootElement.GetProperty("success").GetBoolean());
                    payload = body.RootElement.GetProperty("payload").Clone();
                    await route.FulfillAsync(new() { Response = response });
                });
                var option = Page.GetByRole(AriaRole.Button, new() { Name = $"Select {method}", Exact = true });
                await option.FocusAsync();
                // Queue two clicks in the same turn to exercise the component's early busy guard.
                await option.EvaluateAsync("button => { button.click(); button.click(); }");
                await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
                var dialog = Page.Locator("dialog[open]");
                await Expect(dialog.GetByRole(AriaRole.Status)).ToContainTextAsync("Processing");
                await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true })).ToBeDisabledAsync();
                await Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true })).ToBeDisabledAsync();
                await Page.Keyboard.PressAsync("Escape");
                await Expect(dialog).ToBeVisibleAsync();
                await CaptureAllAsync(method == "Bank Transfer" ? "bank-processing" : "cod-processing");
                release.TrySetResult();
                if (method == "Bank Transfer")
                {
                    await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Bank Transfer Instructions", Exact = true })).ToBeVisibleAsync();
                    var bank = payload.GetProperty("bankTransfer");
                    foreach (var field in new[] { "bankName", "beneficiary", "iban", "reference", "currency", "additionalInfo" })
                        await Expect(Page.Locator("dialog[open]")).ToContainTextAsync(bank.GetProperty(field).GetString()!);
                    Assert.Equal("Synthetic browser bank", bank.GetProperty("bankName").GetString());
                    await Expect(Page.Locator("dialog[open]")).ToContainTextAsync(bank.GetProperty("amount").GetDecimal().ToString("F2", CultureInfo.InvariantCulture));
                    await CaptureAllAsync("bank-instructions");
                    await AssertDialogKeyboardAsync();
                    await Page.GetByRole(AriaRole.Button, new() { Name = "Done", Exact = true }).ClickAsync();
                    await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Bank transfer: payment expected", Exact = true })).ToBeVisibleAsync();
                }
                else
                {
                    await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Cash on delivery", Exact = true })).ToBeVisibleAsync();
                    await Expect(Page.Locator("main")).ToContainTextAsync("not a paid status");
                }
                await Page.UnrouteAsync("**/api/cart/checkout");
                Assert.Equal(1, submissions);
                Assert.DoesNotContain(await Context.CookiesAsync(application.WebBaseUrl.ToString()), cookie => cookie.Name == "my-cart");
                var orderId = payload.GetProperty("orderId").GetGuid();
                await application.WithAdminDatabaseAsync(async db =>
                {
                    var order = await db.Orders.SingleAsync(order => order.Id == orderId);
                    Assert.Equal(OrderStatus.Confirmed, order.OrderStatus);
                    Assert.Equal(OrderPaymentStatus.Pending, order.PaymentStatus);
                    Assert.Equal(FulfillmentStatus.NotStarted, order.FulfillmentStatus);
                    Assert.Equal(data.VariantPrice * 2, order.TotalAmount);
                    var reservation = await db.InventoryReservations.SingleAsync(row => row.OrderId == orderId);
                    Assert.Equal(method == "Bank Transfer" ? InventoryReservationStatus.Reserved : InventoryReservationStatus.Consumed, reservation.Status);
                    Assert.Empty(await db.PaymentTransactions.Where(row => row.OrderId == orderId).ToListAsync());
                    Assert.Single(await db.Orders.Where(order => order.UserId == data.UserId).ToListAsync());
                });
                await Page.GotoAsync(new Uri(application.WebBaseUrl, "authentication/logout").ToString());
                await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Sign in", Exact = true })).ToBeVisibleAsync();
            }
            finally { await application.RemoveScenarioAsync(data); }
        }
    }

    [Fact]
    public async Task CheckoutFeedback_LoadingUnavailableInvalidCartAndApiRecovery()
    {
        var data = await application.SeedScenarioAsync();
        try
        {
            await LoginCheckoutAsync(data);
            await CaptureAllAsync("checkout-empty");
            await SetCartAsync(data);
            await Page.RouteAsync("**/api/payment/methods", route => route.FulfillAsync(new() { Status = 503, ContentType = "application/json", Body = "{}" }));
            await Page.ReloadAsync();
            await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Retry payment methods", Exact = true })).ToBeVisibleAsync();
            await CaptureAllAsync("methods-error");
            await Page.UnrouteAsync("**/api/payment/methods");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Retry payment methods", Exact = true }).ClickAsync();
            await OpenPaymentAsync();
            await AssertDialogKeyboardAsync();
            await Page.Keyboard.PressAsync("Escape");
            await Expect(Page.Locator("dialog[open]")).ToHaveCountAsync(0);
            await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Checkout", Exact = true })).ToBeFocusedAsync();
            await OpenPaymentAsync();
            await Page.RouteAsync("**/api/cart/checkout", route => route.FulfillAsync(new() { Status = 500, ContentType = "application/json", Body = "{\"success\":false,\"message\":\"Synthetic checkout failure\"}" }));
            await Page.GetByRole(AriaRole.Button, new() { Name = "Select Cash on Delivery", Exact = true }).ClickAsync();
            await Expect(Page.Locator("dialog[open]").GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
            await CaptureAllAsync("checkout-submit-error");
            await Page.UnrouteAsync("**/api/cart/checkout");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
            await Page.RouteAsync("**/api/product/single/*", route => route.FulfillAsync(new() { Status = 503, ContentType = "application/json", Body = "{}" }));
            await Page.ReloadAsync();
            await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Retry", Exact = true })).ToBeVisibleAsync();
            await CaptureAllAsync("cart-api-error");
            await Page.UnrouteAsync("**/api/product/single/*");
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await Page.RouteAsync("**/api/product/single/*", async route => { await release.Task.WaitAsync(TimeSpan.FromSeconds(30)); await route.ContinueAsync(); });
            await Page.GetByRole(AriaRole.Button, new() { Name = "Retry", Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Loading your cart" })).ToBeVisibleAsync();
            await CaptureAllAsync("cart-loading");
            release.TrySetResult();
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My Cart", Exact = true })).ToBeVisibleAsync();
            await Page.UnrouteAsync("**/api/product/single/*");
            await Context.AddCookiesAsync([new Cookie { Name = "my-cart", Url = application.WebBaseUrl.ToString(), Value = $"[null,{{\"ProductId\":\"{Guid.NewGuid()}\",\"Quantity\":1}}]" }]);
            await Page.ReloadAsync();
            await Expect(Page.GetByText("Unavailable in catalog", new() { Exact = true })).ToBeVisibleAsync();
            await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Checkout", Exact = true })).ToBeDisabledAsync();
            await Expect(Page.GetByRole(AriaRole.Alert).First).ToBeVisibleAsync();
            await CaptureAllAsync("cart-invalid-unavailable");
            await Page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Remove ") }).ClickAsync();
            await Expect(Page.GetByText("Your cart is empty", new() { Exact = true })).ToBeVisibleAsync();
        }
        finally { await application.RemoveScenarioAsync(data); }
    }

    [Fact]
    public async Task PaymentReturns_QueryParametersCannotMutateOrdersOrInventory()
    {
        var data = await application.SeedScenarioAsync();
        try
        {
            var orderId = await application.SeedAccountOrderAsync(data);
            await LoginCheckoutAsync(data);
            await SetCartAsync(data);
            var before = await DatabaseSnapshotAsync(data);
            var posts = 0;
            Page.Request += (_, request) => { if (request.Method == "POST" && request.Url.Contains("/api/cart/", StringComparison.Ordinal)) posts++; };
            foreach (var path in new[]
            {
                $"payment-success?pm=card&order_id={orderId}&reference=SYNTHETIC&session_id=spoofed&paid=true",
                $"payment-success?pm=stripe&order_id={orderId}&session_id=spoofed",
                $"payment-success/1?order_id={orderId}",
                $"payment-success?pm=unknown&order_id={orderId}",
                $"payment-cancel?order_id={orderId}&session_id=spoofed",
            })
            {
                await Page.GotoAsync(new Uri(application.WebBaseUrl, path).ToString());
                await Expect(Page.Locator("h1")).ToBeVisibleAsync();
                await Expect(Page.Locator("meta[name='robots']")).ToHaveAttributeAsync("content", "noindex,nofollow");
                Assert.DoesNotContain("Payment completed", await Page.Locator("main").InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
                if (path.Contains("pm=card", StringComparison.Ordinal) || path.Contains("pm=stripe", StringComparison.Ordinal))
                    await Expect(Page.Locator("main")).ToContainTextAsync("awaiting server confirmation");
                if (path.StartsWith("payment-success/1", StringComparison.Ordinal))
                    await Expect(Page.Locator("main")).ToContainTextAsync("payment expected");
                await Page.ReloadAsync();
            }
            Assert.Equal(before, await DatabaseSnapshotAsync(data));
            Assert.Equal(0, posts);
            Assert.Contains(await Context.CookiesAsync(application.WebBaseUrl.ToString()), cookie => cookie.Name == "my-cart");
            await Expect(Page.GetByRole(AriaRole.Link, new() { Name = "View orders", Exact = true })).ToHaveAttributeAsync("href", "/account/orders");
        }
        finally { await application.RemoveScenarioAsync(data); }
    }

    [Fact]
    public async Task PublicInformation_StaticSsrSeoAndKeyboardWithoutJavaScript()
    {
        await using var context = await Browser.NewContextAsync(new() { IgnoreHTTPSErrors = true, JavaScriptEnabled = false });
        var page = await context.NewPageAsync();
        foreach (var path in new[] { "about-us", "customer-service", "faq", "privacy", "terms", "unknown-public-page" })
        {
            var response = await page.GotoAsync(new Uri(application.StorefrontBaseUrl, path).ToString());
            Assert.Equal(path == "unknown-public-page" ? 404 : 200, response!.Status);
            await Expect(page.Locator("h1")).ToBeVisibleAsync();
            if (path != "unknown-public-page")
            {
                await Expect(page.Locator("link[rel='canonical']")).ToHaveCountAsync(1);
                Assert.EndsWith('/' + path, await page.Locator("link[rel='canonical']").GetAttributeAsync("href"));
                await Expect(page.Locator("script[type='application/ld+json']")).ToHaveCountAsync(1);
            }
            else
            {
                await Expect(page.Locator("link[rel='canonical']")).ToHaveCountAsync(0);
                await Expect(page.Locator("meta[name='robots']")).ToHaveAttributeAsync("content", "noindex,nofollow");
                Assert.Contains("no-store", response.Headers["cache-control"]);
            }
            if (path == "faq")
            {
                var summary = page.Locator("summary").Filter(new() { HasText = "What payment methods" });
                await summary.FocusAsync();
                await summary.PressAsync("Space");
                await Expect(page.Locator("details[open]").Filter(new() { HasText = "What payment methods" })).ToBeVisibleAsync();
                await summary.PressAsync("Space");
                await Expect(page.Locator("details[open]").Filter(new() { HasText = "What payment methods" })).ToHaveCountAsync(0);
            }
            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                await page.SetViewportSizeAsync(width, 1000);
                // With JS disabled, native FAQ and links still function; HTML/HTTP assertions above do not require hydration.
                var directory = Path.Combine(application.ArtifactDirectory, "ui-v2-checkout-public", "no-js");
                Directory.CreateDirectory(directory);
                await page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"{path}-{width}.png"), FullPage = true });
            }
        }
    }

    [Fact]
    public async Task CheckoutStripe_SyntheticRedirectRetainsPendingPresentation()
    {
        var data = await application.SeedScenarioAsync();
        try
        {
            await LoginCheckoutAsync(data);
            await SetCartAsync(data);
            var before = await DatabaseSnapshotAsync(data);
            var orderId = Guid.NewGuid();
            var redirect = new Uri(application.WebBaseUrl, $"payment-success?pm=card&order_id={orderId}&session_id=synthetic").ToString();
            // Only the provider-facing presentation boundary is doubled. No Stripe request or production bypass is introduced.
            await Page.RouteAsync("**/api/cart/checkout", route => route.FulfillAsync(new()
            {
                Status = 200, ContentType = "application/json",
                Body = JsonSerializer.Serialize(new
                {
                    success = true,
                    payload = new { orderId, orderReference = "SYNTHETIC-STRIPE", status = "PendingPayment", paymentKind = "Stripe", redirectUrl = redirect },
                }),
            }));
            await OpenPaymentAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Select Credit Card", Exact = true }).ClickAsync();
            await Expect(Page).ToHaveURLAsync(redirect);
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Card payment: awaiting server confirmation", Exact = true })).ToBeVisibleAsync();
            await CaptureAllAsync("stripe-redirect-pending");
            Assert.DoesNotContain(await Context.CookiesAsync(application.WebBaseUrl.ToString()), cookie => cookie.Name == "my-cart");
            Assert.Equal(before, await DatabaseSnapshotAsync(data));
        }
        finally { await application.RemoveScenarioAsync(data); }
    }

    private async Task LoginCheckoutAsync(E2ETestData data)
    {
        await Page.GotoAsync(new Uri(application.WebBaseUrl, "authentication/login/account/checkout").ToString());
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Sign in", Exact = true })).ToBeVisibleAsync();
        await Page.GetByLabel("Email Address", new() { Exact = true }).FillAsync(data.UserEmail);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(data.UserPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login", Exact = true }).ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "My Cart", Exact = true })).ToBeVisibleAsync();
    }

    private async Task SetCartAsync(E2ETestData data)
    {
        await Context.AddCookiesAsync([new Cookie { Name = "my-cart", Url = application.WebBaseUrl.ToString(), Value = JsonSerializer.Serialize(new[] { new { ProductId = data.VariantProductId, VariantId = data.VariantId, Quantity = 2 } }) }]);
        await Page.ReloadAsync();
        await Expect(Page.GetByRole(AriaRole.Row).Filter(new() { HasText = data.VariantProductName })).ToBeVisibleAsync();
    }

    private async Task OpenPaymentAsync()
    {
        var button = Page.GetByRole(AriaRole.Button, new() { Name = "Checkout", Exact = true });
        await Expect(button).ToBeEnabledAsync();
        await button.ClickAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Select Payment Method", Exact = true })).ToBeVisibleAsync();
    }

    private async Task AssertDialogKeyboardAsync()
    {
        var dialog = Page.Locator("dialog[open]");
        var buttons = dialog.GetByRole(AriaRole.Button);
        await buttons.Last.FocusAsync();
        await Page.Keyboard.PressAsync("Tab");
        await Expect(buttons.First).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("Shift+Tab");
        await Expect(buttons.Last).ToBeFocusedAsync();
        Assert.True(await buttons.Last.EvaluateAsync<bool>("e => e.matches(':focus-visible') && parseFloat(getComputedStyle(e).outlineWidth) >= 2"));
    }

    private async Task<string> DatabaseSnapshotAsync(E2ETestData data)
    {
        string snapshot = string.Empty;
        await application.WithAdminDatabaseAsync(async db =>
        {
            var orders = await db.Orders.AsNoTracking().Where(o => o.UserId == data.UserId).OrderBy(o => o.Id).ToListAsync();
            var ids = orders.Select(o => o.Id).ToArray();
            snapshot = JsonSerializer.Serialize(new
            {
                Orders = orders,
                Reservations = await db.InventoryReservations.AsNoTracking().Where(r => ids.Contains(r.OrderId)).OrderBy(r => r.Id).ToListAsync(),
                Payments = await db.PaymentTransactions.AsNoTracking().Where(p => ids.Contains(p.OrderId)).OrderBy(p => p.Id).ToListAsync(),
                Products = await db.Products.AsNoTracking().Where(p => p.CategoryId == data.CategoryId).OrderBy(p => p.Id).Select(p => new { p.Id, p.Quantity }).ToListAsync(),
                Variants = await db.ProductVariants.AsNoTracking().Where(v => v.ProductId == data.VariantProductId).OrderBy(v => v.Id).Select(v => new { v.Id, v.Stock }).ToListAsync(),
            });
        });
        return snapshot;
    }
}
