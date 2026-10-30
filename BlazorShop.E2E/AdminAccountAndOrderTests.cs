namespace BlazorShop.E2E;

using System.Globalization;
using System.Text.RegularExpressions;
using BlazorShop.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

public sealed partial class AdminOperationsTests
{
    [Fact]
    public async Task AdminDashboardAndOrders_MetricsExportAndTracking()
    {
        var data = await application.SeedScenarioAsync();
        await application.GrantScenarioAdminAsync(data);
        var orderId = await application.SeedAccountOrderAsync(data);
        decimal revenue = 0;
        await application.WithAdminDatabaseAsync(async db => revenue = await db.Orders.Where(o => o.PaymentStatus == OrderPaymentStatus.Paid).SumAsync(o => o.TotalAmount));
        try
        {
            await LoginAsync(data);
            await Expect(Page.Locator(".bs-metric-card").Filter(new() { HasText = "Total Revenue" })).ToContainTextAsync(revenue.ToString("F2", CultureInfo.InvariantCulture));
            await Button(Page.Locator("main"), "7 d").ClickAsync();
            await Page.GetByLabel("Metrics granularity").SelectOptionAsync("week");
            await Expect(Page.GetByText("Loading metrics…", new() { Exact = true })).ToHaveCountAsync(0);
            var download = await Page.RunAndWaitForDownloadAsync(() => Button(Page.Locator("main"), "Export CSV").ClickAsync());
            Assert.EndsWith(".csv", download.SuggestedFilename, StringComparison.Ordinal);
            var downloadPath = await download.PathAsync();
            Assert.Contains($"UI-{data.SimpleProductSlug}", await File.ReadAllTextAsync(downloadPath!));
            await GoAsync("admin/orders");
            await Page.GetByLabel("Search name/email").FillAsync(data.UserEmail);
            await Page.Locator("main h1").ClickAsync();
            await Button(Page.Locator("main"), "Expand All").ClickAsync();
            await Button(Page.Locator("main"), "Details").ClickAsync();
            await Expect(Dialog).ToContainTextAsync(data.VariantSku);
            await Expect(Dialog).ToContainTextAsync("62.50");
            await CaptureWidthsAsync("order-details");
            await Button(Dialog, "Close").Last.ClickAsync();
            await Button(Page.Locator("main"), "Edit").ClickAsync();
            await Dialog.GetByLabel("Carrier", new() { Exact = true }).FillAsync("Synthetic carrier");
            await Dialog.GetByLabel("Tracking Number").FillAsync("UI-TRACK-42");
            await Dialog.GetByLabel("Tracking Url").FillAsync("https://example.com/track/UI-TRACK-42");
            await Dialog.GetByLabel("Fulfillment status").SelectOptionAsync("InTransit");
            await Dialog.GetByLabel("Admin note").FillAsync("Synthetic browser regression note.");
            await CaptureWidthsAsync("order-edit-tracking");
            await Button(Dialog, "Save").ClickAsync();
            await Expect(Dialog).ToHaveCountAsync(0);
            await Expect(Page.Locator("main")).ToContainTextAsync("InTransit");
            await application.WithAdminDatabaseAsync(async db =>
            {
                var order = await db.Orders.Include(o => o.Lines).SingleAsync(o => o.Id == orderId);
                Assert.Equal("UI-TRACK-42", order.TrackingNumber);
                Assert.Equal(FulfillmentStatus.InTransit, order.FulfillmentStatus);
                Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
                Assert.Equal(OrderStatus.Confirmed, order.OrderStatus);
                Assert.Equal(62.50m, order.TotalAmount);
                Assert.Equal(data.VariantSku, Assert.Single(order.Lines).SkuSnapshot);
            });
            await Button(Page.Locator("main"), "Export CSV").ClickAsync();
            var csv = await Page.EvaluateAsync<string>("navigator.clipboard.readText()");
            Assert.Contains("OrderStatus,PaymentStatus,FulfillmentStatus", csv);
            Assert.Contains("UI-TRACK-42", csv);
            Assert.Contains(data.UserEmail, csv);
        }
        finally { await application.RemoveScenarioAsync(data); }
    }

    [Fact]
    public async Task AdminUsers_SearchRolesLockAndAudit()
    {
        var admin = await application.SeedScenarioAsync();
        var customer = await application.SeedScenarioAsync();
        await application.GrantScenarioAdminAsync(admin);
        await application.WithAdminDatabaseAsync(async db => { (await db.Users.SingleAsync(u => u.Id == customer.UserId)).EmailConfirmed = false; await db.SaveChangesAsync(); });
        try
        {
            await LoginAsync(admin);
            await GoAsync("admin/users");
            await Page.GetByLabel("Search email or name").FillAsync(customer.UserEmail);
            await Button(Page.Locator("main"), "Apply").ClickAsync();
            var row = Row(customer.UserId);
            await Button(row, "Details").ClickAsync();
            await Expect(Dialog).ToContainTextAsync(customer.UserEmail);
            await CaptureWidthsAsync("user-details");
            await Button(Dialog, "Close").Last.ClickAsync();
            await Button(row, "Roles").ClickAsync();
            await Dialog.GetByLabel("Admin", new() { Exact = true }).CheckAsync();
            await CaptureWidthsAsync("user-roles");
            await Button(Dialog, "Save").ClickAsync();
            await Expect(Dialog).ToHaveCountAsync(0);
            await Expect(row.Locator("td[data-label=Roles]")).ToContainTextAsync("Admin");
            await Button(row, "Roles").ClickAsync();
            await Dialog.GetByLabel("Admin", new() { Exact = true }).UncheckAsync();
            await Button(Dialog, "Save").ClickAsync();
            await Expect(Dialog).ToHaveCountAsync(0);
            await Button(row, "Lock").ClickAsync();
            await Expect(Button(row, "Unlock")).ToBeVisibleAsync();
            await Button(row, "Unlock").ClickAsync();
            await Expect(Button(row, "Lock")).ToBeVisibleAsync();
            await Button(row, "Confirm email").ClickAsync();
            await Expect(Button(row, "Confirm email")).ToBeDisabledAsync();
            await Button(row, "Require password").ClickAsync();
            await Expect(row).ToContainTextAsync("Password change");
            await Button(row, "Deactivate").ClickAsync();
            await Expect(Button(row, "Unlock")).ToBeVisibleAsync();
            await application.WithAdminDatabaseAsync(async db =>
            {
                var user = await db.Users.SingleAsync(u => u.Id == customer.UserId);
                Assert.True(user.EmailConfirmed);
                Assert.True(user.RequirePasswordChange);
                Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);
                Assert.True(await db.AdminAuditLogs.AnyAsync(a => a.ActorUserId == admin.UserId && a.EntityId == customer.UserId));
            });
            await GoAsync("admin/audit");
            await Page.GetByLabel("Actor", new() { Exact = true }).FillAsync(admin.UserEmail);
            await Button(Page.Locator("main"), "Apply").ClickAsync();
            await Expect(Page.Locator("tbody tr").First).ToContainTextAsync(admin.UserEmail);
            await Button(Page.Locator("tbody tr").First, "Details").ClickAsync();
            await Expect(Dialog).ToContainTextAsync(admin.UserEmail);
            await Expect(Dialog.Locator("input, textarea, select")).ToHaveCountAsync(0);
            await CaptureWidthsAsync("audit-details");
            await Page.Keyboard.PressAsync("Escape");
            await Page.GetByLabel("Action", new() { Exact = true }).FillAsync("no-matching-action");
            await Button(Page.Locator("main"), "Apply").ClickAsync();
            await Expect(Page.GetByText("No audit entries found.", new() { Exact = true })).ToBeVisibleAsync();
        }
        finally { await application.RemoveScenarioAsync(customer); await application.RemoveScenarioAsync(admin); }
    }

    [Fact]
    public async Task AdminAuthorization_CustomerDeniedAndDemoSessionBound()
    {
        var customer = await application.SeedScenarioAsync();
        try
        {
            await GoAsync("authentication/login");
            await Page.GetByLabel("Email Address", new() { Exact = true }).FillAsync(customer.UserEmail);
            await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(customer.UserPassword);
            await Button(Page.Locator("main"), "Login").ClickAsync();
            await Expect(Page).ToHaveURLAsync(new Regex("/account$"));
            var headers = await HeadersAsync();
            foreach (var path in new[] { "admin", "admin/products", "admin/categories", "admin/inventory", "admin/orders", "admin/users", "admin/seo", "admin/redirects", "admin/settings", "admin/audit" })
            {
                await GoAsync(path);
                await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Access denied", Exact = true })).ToBeVisibleAsync();
            }
            foreach (var path in new[] { "admin/inventory", "admin/orders", "admin/users", "admin/seo/settings", "admin/seo/redirects", "admin/settings", "admin/audit" })
            {
                var denied = await Context.APIRequest.GetAsync(new Uri(application.ApiBaseUrl, "api/" + path).ToString(), new() { Headers = headers });
                Assert.Equal(403, denied.Status);
                await denied.DisposeAsync();
            }
            await GoAsync("authentication/logout");
            await Expect(Page).ToHaveURLAsync(new Regex("/authentication/login$"));
            await Page.GetByText("Explore a demo workspace", new() { Exact = true }).ClickAsync();
            await Button(Page.Locator("main"), "Administrator demo").ClickAsync();
            await Expect(Page).ToHaveURLAsync(new Regex("/admin$"));
            headers = await HeadersAsync();
            var endpoint = new Uri(application.ApiBaseUrl, "api/admin/inventory").ToString();
            var active = await Context.APIRequest.GetAsync(endpoint, new() { Headers = headers });
            Assert.Equal(200, active.Status);
            await active.DisposeAsync();
            var isolated = await Playwright.APIRequest.NewContextAsync(new() { IgnoreHTTPSErrors = true });
            try
            {
                var replay = await isolated.GetAsync(endpoint, new() { Headers = headers });
                Assert.Equal(401, replay.Status);
                await replay.DisposeAsync();
            }
            finally { await isolated.DisposeAsync(); }
            await GoAsync("authentication/logout");
            await Expect(Page).ToHaveURLAsync(new Regex("/authentication/login$"));
            var ended = await Context.APIRequest.GetAsync(endpoint, new() { Headers = headers });
            Assert.Equal(401, ended.Status);
            await ended.DisposeAsync();
        }
        finally { await application.RemoveScenarioAsync(customer); }
    }
}
