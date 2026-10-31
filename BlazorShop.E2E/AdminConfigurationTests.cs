namespace BlazorShop.E2E;

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;

public sealed partial class AdminOperationsTests
{
    [Fact]
    public async Task AdminSeo_MetadataAndRedirectCrud()
    {
        var data = await application.SeedScenarioAsync();
        await application.GrantScenarioAdminAsync(data);
        var oldPath = $"/ui-v2-{Guid.NewGuid():N}";
        JsonElement? baseline = null;
        try
        {
            await LoginAsync(data);
            baseline = await ReadApiAsync("seo/settings");
            await GoAsync("admin/seo");
            await Page.GetByLabel("Site Name", new() { Exact = true }).FillAsync("Synthetic browser store");
            await Page.GetByLabel("Default Title Suffix").FillAsync("UI regression");
            await SaveAndCheckAsync("Save Settings", "admin/seo/settings");
            var saved = await ReadApiAsync("seo/settings");
            Assert.Equal("Synthetic browser store", saved.GetProperty("siteName").GetString());
            await GoAsync("admin/redirects");
            await Button(Page.Locator("main"), "Add Redirect").ClickAsync();
            await Dialog.GetByLabel("Old Path").FillAsync(oldPath);
            await Dialog.GetByLabel("New Path").FillAsync("/new-releases");
            await CaptureWidthsAsync("redirect-create");
            await Button(Dialog, "Save").ClickAsync();
            await Expect(Row(oldPath)).ToBeVisibleAsync();
            await Button(Row(oldPath), "Edit").ClickAsync();
            await Dialog.GetByLabel("New Path").FillAsync("/today-deals");
            await Dialog.GetByLabel("Status Code").SelectOptionAsync("302");
            await Button(Dialog, "Save").ClickAsync();
            await Expect(Dialog).ToHaveCountAsync(0);
            await Expect(Row(oldPath)).ToContainTextAsync("302");
            await Expect(Row(oldPath)).ToContainTextAsync("/today-deals");
            await Button(Row(oldPath), "Deactivate").ClickAsync();
            await Expect(Row(oldPath)).ToContainTextAsync("Inactive");
            await Button(Row(oldPath), "Edit").ClickAsync();
            await Dialog.GetByLabel("Active", new() { Exact = true }).CheckAsync();
            await Button(Dialog, "Save").ClickAsync();
            await Expect(Button(Row(oldPath), "Deactivate")).ToBeVisibleAsync();
            await Button(Row(oldPath), "Delete").ClickAsync();
            await CaptureWidthsAsync("redirect-delete-confirmation");
            await Button(Dialog, "Delete").ClickAsync();
            await Expect(Row(oldPath)).ToHaveCountAsync(0);
        }
        finally
        {
            try { if (baseline.HasValue) await RestoreApiAsync("admin/seo/settings", baseline.Value); }
            finally
            {
                await application.WithAdminDatabaseAsync(async db => { db.SeoRedirects.RemoveRange(await db.SeoRedirects.Where(r => r.OldPath == oldPath).ToListAsync()); await db.SaveChangesAsync(); });
                await application.RemoveScenarioAsync(data);
            }
        }
    }

    [Fact]
    public async Task AdminSettings_StoreOrdersNotificationsAndReadOnlySystem()
    {
        var data = await application.SeedScenarioAsync();
        await application.GrantScenarioAdminAsync(data);
        JsonElement? baseline = null;
        try
        {
            await LoginAsync(data);
            baseline = await ReadApiAsync("admin/settings");
            await GoAsync("admin/settings");
            await Page.GetByLabel("Store name", new() { Exact = true }).FillAsync("Synthetic administration store");
            await SaveAndCheckAsync("Save Store", "admin/settings/store");
            Assert.Equal("Synthetic administration store", (await ReadApiAsync("admin/settings")).GetProperty("store").GetProperty("storeName").GetString());
            await Button(Page.Locator("main"), "Orders").ClickAsync();
            await Expect(Page.GetByLabel("Guest checkout", new() { Exact = true })).ToBeDisabledAsync();
            await Page.GetByLabel("Order reference prefix").FillAsync("UIREG");
            await SaveAndCheckAsync("Save Orders", "admin/settings/orders");
            Assert.Equal("UIREG", (await ReadApiAsync("admin/settings")).GetProperty("orders").GetProperty("orderReferencePrefix").GetString());
            await CaptureWidthsAsync("settings-orders");
            await Button(Page.Locator("main"), "Notifications").ClickAsync();
            await Page.GetByLabel("SMTP host").FillAsync("smtp.example.test");
            await Page.GetByLabel("From email").FillAsync("browser@example.test");
            await Page.GetByLabel("From display name").FillAsync("Synthetic browser store");
            await SaveAndCheckAsync("Save Notifications", "admin/settings/notifications");
            var notification = (await ReadApiAsync("admin/settings")).GetProperty("notifications");
            Assert.Equal("Synthetic browser store", notification.GetProperty("smtpFromDisplayName").GetString());
            Assert.False(notification.TryGetProperty("password", out _));
            await Expect(Page.Locator("main input[type=password]")).ToHaveCountAsync(0);
            await CaptureWidthsAsync("settings-notifications");
            await Button(Page.Locator("main"), "System").ClickAsync();
            await Expect(Page.Locator("main input, main textarea, main select, main form")).ToHaveCountAsync(0);
            await CaptureWidthsAsync("settings-system");
        }
        finally
        {
            try
            {
                if (baseline.HasValue)
                {
                    await RestoreApiAsync("admin/settings/store", baseline.Value.GetProperty("store"));
                    await RestoreApiAsync("admin/settings/orders", baseline.Value.GetProperty("orders"));
                    await RestoreApiAsync("admin/settings/notifications", baseline.Value.GetProperty("notifications"));
                }
            }
            finally { await application.RemoveScenarioAsync(data); }
        }
    }

    [Fact]
    public async Task AdminFeedback_LoadingEmptyFailureAndRetry()
    {
        var data = await application.SeedScenarioAsync();
        await application.GrantScenarioAdminAsync(data);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var intercepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var usersQuery = new Regex(@"/api/admin/users\?");
        try
        {
            await LoginAsync(data);
            await GoAsync("admin/inventory");
            await FilterInventoryAsync($"no-product-{Guid.NewGuid():N}");
            await Expect(Page.GetByText("No inventory items found.", new() { Exact = true })).ToBeVisibleAsync();
            await CaptureWidthsAsync("inventory-empty");
            await GoAsync("admin/users");
            await Expect(Page.Locator("tbody tr").First).ToBeVisibleAsync();
            await Page.RouteAsync(usersQuery, async route =>
            {
                if (route.Request.Method != "GET") { await route.ContinueAsync(); return; }
                intercepted.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(60));
                await route.AbortAsync();
            });
            await Button(Page.Locator("main"), "Apply").ClickAsync();
            await intercepted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Expect(Page.GetByRole(AriaRole.Status).Filter(new() { HasText = "Loading users..." })).ToBeVisibleAsync();
            await CaptureWidthsAsync("users-loading");
            release.TrySetResult();
            await Expect(Button(Page.Locator("main"), "Retry")).ToBeVisibleAsync();
            await Expect(Page.GetByRole(AriaRole.Alert)).ToBeVisibleAsync();
            await CaptureWidthsAsync("users-error");
            await Page.UnrouteAsync(usersQuery);
            await Button(Page.Locator("main"), "Retry").ClickAsync();
            await Expect(Page.Locator("tbody tr").First).ToBeVisibleAsync();
            await Page.GetByLabel("Search email or name").FillAsync($"no-user-{Guid.NewGuid():N}");
            await Button(Page.Locator("main"), "Apply").ClickAsync();
            await Expect(Page.GetByText("No users found.", new() { Exact = true })).ToBeVisibleAsync();
            await CaptureWidthsAsync("users-empty");
        }
        finally { release.TrySetResult(); await application.RemoveScenarioAsync(data); }
    }

    private async Task<JsonElement> ReadApiAsync(string route)
    {
        var response = await Context.APIRequest.GetAsync(new Uri(application.ApiBaseUrl, "api/" + route).ToString(), new() { Headers = await HeadersAsync() });
        Assert.Equal(200, response.Status);
        var result = (await response.JsonAsync())!.Value.Clone();
        await response.DisposeAsync();
        return result;
    }

    private async Task RestoreApiAsync(string route, JsonElement data)
    {
        var headers = await HeadersAsync();
        headers["Content-Type"] = "application/json";
        var response = await Context.APIRequest.PutAsync(new Uri(application.ApiBaseUrl, "api/" + route).ToString(), new() { Headers = headers, DataString = data.GetRawText() });
        Assert.Equal(200, response.Status);
        await response.DisposeAsync();
    }

    private async Task SaveAndCheckAsync(string button, string route)
    {
        var saved = Page.WaitForResponseAsync(r => r.Url.EndsWith("/api/" + route, StringComparison.Ordinal) && r.Request.Method == "PUT");
        await Button(Page.Locator("main"), button).ClickAsync();
        Assert.Equal(200, (await saved).Status);
        await Expect(Button(Page.Locator("main"), button)).ToBeEnabledAsync();
    }
}
