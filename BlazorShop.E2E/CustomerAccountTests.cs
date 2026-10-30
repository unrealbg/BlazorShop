namespace BlazorShop.E2E;

using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

[Collection(E2ECollection.Name)]
public sealed class CustomerAccountTests(E2EApplicationFixture application) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new() { IgnoreHTTPSErrors = true, Locale = "en-US" };

    [Fact]
    public async Task AccessForms_ResponsiveValidationRegistrationAndConfirmation()
    {
        var data = await application.SeedScenarioAsync();
        try
        {
            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                foreach (var (path, state) in new[] { ("", "workspace"), ("authentication/login", "login"), ("authentication/register", "register"), ("confirm-email", "confirmation-invalid") })
                {
                    await Page.SetViewportSizeAsync(width, 1000);
                    await GoAsync(path);
                    await Expect(Page.Locator("h1")).ToBeVisibleAsync();
                    if (path == "confirm-email") await Expect(Page.GetByRole(AriaRole.Status)).ToContainTextAsync("Invalid confirmation link.");
                    await CaptureAsync(state, width);
                }
            }

            await GoAsync("authentication/register");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Register", Exact = true }).ClickAsync();
            await Expect(Page.Locator(".validation-message").First).ToBeVisibleAsync();
            await Page.GetByLabel("Full Name", new() { Exact = true }).FillAsync("Browser Registration");
            await Page.GetByLabel("Email Address", new() { Exact = true }).FillAsync(data.UserEmail);
            await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(data.UserPassword);
            await Page.GetByLabel("Confirm Password", new() { Exact = true }).FillAsync(data.UserPassword);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Register", Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Alert).Filter(new() { HasText = "User already exists" })).ToBeVisibleAsync();
            var email = $"registration.{Guid.NewGuid():N}@blazorshop.local";
            await Page.GetByLabel("Email Address", new() { Exact = true }).FillAsync(email);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Register", Exact = true }).ClickAsync();
            await Expect(Page).ToHaveURLAsync(new Regex("/authentication/login/account$"));
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Sign in", Exact = true })).ToBeVisibleAsync();

            // UI state coverage only: the actual Identity confirmation rules remain covered by backend tests.
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await Page.RouteAsync("**/api/authentication/confirm-email?*", async route =>
            {
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                await route.FulfillAsync(new() { Status = 200, ContentType = "application/json", Body = "{\"success\":true,\"message\":\"Confirmed\"}" });
            });
            await GoAsync("confirm-email?userId=synthetic&token=synthetic");
            await Expect(Page.GetByRole(AriaRole.Status)).ToContainTextAsync("Processing your request");
            await CaptureAsync("confirmation-loading", 1440);
            release.SetResult();
            await Expect(Page.GetByRole(AriaRole.Status)).ToContainTextAsync("Your email has been confirmed successfully.");
            await CaptureAsync("confirmation-success", 1440);
            await Page.UnrouteAsync("**/api/authentication/confirm-email?*");
            await GoAsync($"confirm-email?userId={data.UserId}&token=synthetic-invalid");
            await Expect(Page.GetByRole(AriaRole.Status)).ToContainTextAsync("Failed to confirm email:");
            await CaptureAsync("confirmation-rejected", 320);
        }
        finally { await application.RemoveScenarioAsync(data); }
    }

    [Fact]
    public async Task CustomerAccount_ResponsiveOrdersProfilePasswordAndNotifications()
    {
        var data = await application.SeedScenarioAsync();
        var orderId = await application.SeedAccountOrderAsync(data);
        try
        {
            await LoginAsync(data);
            await GoAsync("admin");
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Access denied", Exact = true })).ToBeVisibleAsync();
            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                await Page.SetViewportSizeAsync(width, 1000);
                foreach (var (path, state) in new[] { ("account", "overview"), ("account/orders", "orders"), ($"account/orders/{orderId:D}", "order-detail"), ("account/profile", "profile"), ("account/settings", "settings"), ("account/notifications", "notifications") })
                {
                    await GoAsync(path);
                    await Expect(Page.Locator("#account-content h1")).ToBeVisibleAsync();
                    if (state is "orders" or "order-detail" or "overview")
                    {
                        await Expect(Page.Locator("#account-content")).ToContainTextAsync(data.VariantProductName);
                        await Expect(Page.Locator("#account-content")).ToContainTextAsync("Paid");
                        await Expect(Page.Locator("#account-content")).ToContainTextAsync("Shipped");
                    }
                    if (state == "order-detail")
                    {
                        await Expect(Page.Locator("table")).ToContainTextAsync(data.VariantSize);
                        await Expect(Page.Locator("table")).ToContainTextAsync("62.50");
                    }
                    await CaptureAsync(state, width);
                }
            }

            await GoAsync("account/profile");
            await Page.GetByLabel("Full Name", new() { Exact = true }).FillAsync("Updated Browser Customer");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Save changes", Exact = true }).ClickAsync();
            await Expect(Page.GetByText("Updated Browser Customer", new() { Exact = true })).ToBeVisibleAsync();
            await RefreshThroughBrowserAsync("account/profile");
            await Expect(Page.GetByLabel("Full Name", new() { Exact = true })).ToHaveValueAsync("Updated Browser Customer");

            await GoAsync("account/settings");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Update password", Exact = true }).ClickAsync();
            await Expect(Page.Locator(".validation-message").First).ToBeVisibleAsync();
            var password = $"Updated!{Guid.NewGuid():N}aA1";
            await Page.GetByLabel("Current password", new() { Exact = true }).FillAsync(data.UserPassword);
            await Page.GetByLabel("New password", new() { Exact = true }).FillAsync(password);
            await Page.GetByLabel("Confirm new password", new() { Exact = true }).FillAsync("mismatch");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Update password", Exact = true }).ClickAsync();
            await Expect(Page.Locator(".validation-message")).ToContainTextAsync(["Passwords do not match."]);
            await Page.GetByLabel("Confirm new password", new() { Exact = true }).FillAsync(password);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Update password", Exact = true }).ClickAsync();
            await Expect(Page.GetByLabel("Current password", new() { Exact = true })).ToHaveValueAsync("");
            await Page.Locator(".v2-account-nav a[href='/account/notifications']").ClickAsync();
            await Expect(Page.Locator("#account-content")).ToContainTextAsync("Password updated");
            await CaptureAsync("notifications-event", 390);
            await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Mark all as read", Exact = true })).ToBeDisabledAsync();
            await Page.GetByRole(AriaRole.Button, new() { Name = "Clear inbox", Exact = true }).ClickAsync();
            await Expect(Page.GetByText("No notifications yet.", new() { Exact = true })).ToBeVisibleAsync();
            await SignOutAsync();
            await LoginAsync(data with { UserPassword = password });
        }
        finally { await application.RemoveScenarioAsync(data); }
    }

    [Fact]
    public async Task CustomerOrders_LoadingEmptyErrorAndRetry()
    {
        var data = await application.SeedScenarioAsync();
        try
        {
            await LoginAsync(data);
            await GoAsync("account/orders");
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "No orders yet", Exact = true })).ToBeVisibleAsync();
            await CaptureAsync("orders-empty", 390);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await Page.RouteAsync("**/api/cart/user/orders", async route => { await release.Task.WaitAsync(TimeSpan.FromSeconds(30)); await route.AbortAsync(); });
            await GoAsync("account/orders");
            await Expect(Page.GetByRole(AriaRole.Status)).ToContainTextAsync("Loading orders...");
            await CaptureAsync("orders-loading", 390);
            release.SetResult();
            await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Retry", Exact = true })).ToBeVisibleAsync();
            await CaptureAsync("orders-error", 390);
            await Page.UnrouteAsync("**/api/cart/user/orders");
            await Page.GetByRole(AriaRole.Button, new() { Name = "Retry", Exact = true }).ClickAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "No orders yet", Exact = true })).ToBeVisibleAsync();
        }
        finally { await application.RemoveScenarioAsync(data); }
    }

    [Fact]
    public async Task AdminAndDemo_LoginRefreshLogoutAndSessionIsolation()
    {
        var data = await application.SeedScenarioAsync();
        try
        {
            await application.GrantScenarioAdminAsync(data);
            await LoginAsync(data, admin: true);
            await RefreshThroughBrowserAsync("admin");
            await SignOutAsync();
            foreach (var role in new[] { "Customer", "Administrator" })
            {
                await GoAsync("authentication/login");
                await Page.GetByText("Explore a demo workspace", new() { Exact = true }).ClickAsync();
                await Page.GetByRole(AriaRole.Button, new() { Name = $"{role} demo", Exact = true }).ClickAsync();
                var target = role == "Administrator" ? "admin" : "account";
                await Expect(Page).ToHaveURLAsync(new Regex($"/{target}$"));
                if (role == "Customer")
                {
                    await GoAsync("account/orders");
                    await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "No orders yet", Exact = true })).ToBeVisibleAsync();
                }
                await RefreshThroughBrowserAsync(target);
                var token = await Page.EvaluateAsync<string>("sessionStorage.getItem('token')");
                Assert.False(string.IsNullOrWhiteSpace(token));
                var headers = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" };
                var active = await Context.APIRequest.GetAsync(new Uri(application.ApiBaseUrl, "api/cart/user/orders").ToString(), new() { Headers = headers });
                Assert.Equal(200, active.Status);
                await active.DisposeAsync();
                var isolated = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true });
                try
                {
                    var replay = await isolated.GetAsync(new Uri(application.ApiBaseUrl, "api/cart/user/orders").ToString(), new() { Headers = headers });
                    Assert.Equal(401, replay.Status);
                    await replay.DisposeAsync();
                    if (role == "Customer")
                    {
                        await GoAsync("admin");
                        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Access denied", Exact = true })).ToBeVisibleAsync();
                    }
                    await SignOutAsync();
                    var ended = await Context.APIRequest.GetAsync(new Uri(application.ApiBaseUrl, "api/cart/user/orders").ToString(), new() { Headers = headers });
                    Assert.Equal(401, ended.Status);
                    await ended.DisposeAsync();
                }
                finally { await isolated.DisposeAsync(); }
            }
        }
        finally { await application.RemoveScenarioAsync(data); }
    }

    private async Task LoginAsync(E2ETestData data, bool admin = false)
    {
        await GoAsync("authentication/login");
        await Page.GetByLabel("Email Address", new() { Exact = true }).FillAsync(data.UserEmail);
        await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(data.UserPassword);
        await Page.GetByRole(AriaRole.Button, new() { Name = "Login", Exact = true }).ClickAsync();
        await Expect(Page).ToHaveURLAsync(new Regex(admin ? "/admin$" : "/account$"));
    }

    private async Task RefreshThroughBrowserAsync(string path)
    {
        await GoAsync(path);
        var refreshed = Page.WaitForResponseAsync(response => response.Url.EndsWith("/api/authentication/refresh-token", StringComparison.Ordinal) && response.Request.Method == "POST");
        await Page.EvaluateAsync("sessionStorage.removeItem('token')");
        await Page.ReloadAsync();
        Assert.Equal(200, (await refreshed).Status);
        await Expect(Page).ToHaveURLAsync(new Regex($"/{Regex.Escape(path)}$"));
        await Expect(Page.Locator("h1").First).ToBeVisibleAsync();
    }

    private async Task SignOutAsync()
    {
        await GoAsync("authentication/logout");
        await Expect(Page).ToHaveURLAsync(new Regex("/authentication/login/account$"));
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Sign in", Exact = true })).ToBeVisibleAsync();
        Assert.True(await Page.EvaluateAsync<bool>("sessionStorage.getItem('token') === null"));
    }

    private Task GoAsync(string path) => Page.GotoAsync(new Uri(application.WebBaseUrl, path).ToString());

    private async Task CaptureAsync(string state, int width)
    {
        await Page.SetViewportSizeAsync(width, 1000);
        await Page.EvaluateAsync("document.fonts.ready");
        Assert.True(await Page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"), $"Horizontal overflow in {state} at {width}px.");
        Assert.True(await Page.Locator(".v2-workspace input, .v2-auth-card, .v2-account-nav, .v2-account .bs-panel, .v2-account table").EvaluateAllAsync<bool>("elements => elements.every(element => { const bounds = element.getBoundingClientRect(); return bounds.left >= -1 && bounds.right <= innerWidth + 1; })"), $"Clipped content in {state} at {width}px.");
        var skip = Page.GetByRole(AriaRole.Link, new() { Name = "Skip to content", Exact = true });
        await skip.FocusAsync();
        Assert.Equal("solid", await skip.EvaluateAsync<string>("element => getComputedStyle(element).outlineStyle"));
        await skip.PressAsync("Enter");
        await Expect(Page.Locator("main")).ToBeFocusedAsync();
        var directory = Path.Combine(application.ArtifactDirectory, "ui-v2-account");
        Directory.CreateDirectory(directory);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"{state}-{width}.png"), FullPage = true });
    }
}
