namespace BlazorShop.E2E;

using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

[Collection(E2ECollection.Name)]
public sealed class RefreshCoordinationTests(E2EApplicationFixture application) : PageTest
{
    private const string RefreshPath = "**/api/authentication/refresh-token";
    public override BrowserNewContextOptions ContextOptions() => new() { IgnoreHTTPSErrors = true, Locale = "en-US" };

    [Fact]
    public async Task TwoTabs_ConcurrentBootstrapSerializesCookieRotation()
    {
        var data = await application.SeedScenarioAsync();
        var release = Signal();
        try
        {
            await LoginAsync(Page, data);
            var other = await Context.NewPageAsync();
            await GoAsync(other, "account");
            await Expect(other.Locator("#account-content h1")).ToBeVisibleAsync();
            var reached = Signal();
            var requests = 0;
            await Context.RouteAsync(RefreshPath, async route =>
            {
                if (Interlocked.Increment(ref requests) == 1)
                {
                    reached.TrySetResult();
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                await route.ContinueAsync();
            });
            await Task.WhenAll(Page.EvaluateAsync("sessionStorage.removeItem('token')"), other.EvaluateAsync("sessionStorage.removeItem('token')"));
            var firstReload = Page.ReloadAsync();
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var secondReload = other.ReloadAsync();
            await other.WaitForFunctionAsync("async () => (await navigator.locks.query()).pending.some(lock => lock.name === 'blazorshop.authentication')");
            Assert.Equal(1, Volatile.Read(ref requests));
            release.TrySetResult();
            await Task.WhenAll(firstReload, secondReload);
            await Expect(Page.Locator("#account-content h1")).ToBeVisibleAsync();
            await Expect(other.Locator("#account-content h1")).ToBeVisibleAsync();
            Assert.Equal(2, requests); // Each tab needs its own sessionStorage JWT.
            await AssertMetadataOnlyAsync(Page);
            await CaptureAsync("two-tab-bootstrap");
        }
        finally { release.TrySetResult(); await application.RemoveScenarioAsync(data); }
    }

    [Fact]
    public async Task TwoTabs_Concurrent401UsesLatestCookieWithoutReplay()
    {
        var data = await application.SeedScenarioAsync();
        var release = Signal();
        try
        {
            await LoginAsync(Page, data);
            var other = await Context.NewPageAsync();
            await GoAsync(other, "account");
            await Expect(other.Locator("#account-content h1")).ToBeVisibleAsync();
            var arrivals = 0;
            var both = Signal();
            var refreshCount = 0;
            await Context.RouteAsync(RefreshPath, async route => { Interlocked.Increment(ref refreshCount); await route.ContinueAsync(); });
            foreach (var tab in new[] { Page, other })
            {
                var sent401 = false;
                await tab.RouteAsync("**/api/cart/user/orders", async route =>
                {
                    if (!sent401)
                    {
                        sent401 = true;
                        if (Interlocked.Increment(ref arrivals) == 2) both.TrySetResult();
                        await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                        await UnauthorizedAsync(route);
                    }
                    else await route.ContinueAsync();
                });
            }
            await Task.WhenAll(NavigateAsync(Page, "/account/orders"), NavigateAsync(other, "/account/orders"));
            await both.Task.WaitAsync(TimeSpan.FromSeconds(30));
            release.TrySetResult();
            foreach (var tab in new[] { Page, other })
                await Expect(tab.GetByRole(AriaRole.Heading, new() { Name = "No orders yet", Exact = true })).ToBeVisibleAsync();
            Assert.Equal(2, refreshCount);
            await AssertMetadataOnlyAsync(Page);
        }
        finally { release.TrySetResult(); await application.RemoveScenarioAsync(data); }
    }

    [Fact]
    public async Task OneTab_401BurstSharesOneRefreshAndIgnoresDuplicateEvents()
    {
        var data = await application.SeedScenarioAsync();
        var release = Signal();
        try
        {
            await LoginAsync(Page, data);
            var first = Signal();
            var both = Signal();
            var count = 0;
            var refreshCount = 0;
            await Page.RouteAsync(RefreshPath, async route => { Interlocked.Increment(ref refreshCount); await route.ContinueAsync(); });
            await Page.RouteAsync("**/api/cart/user/orders", async route =>
            {
                var arrival = Interlocked.Increment(ref count);
                if (arrival <= 2)
                {
                    first.TrySetResult();
                    if (arrival == 2) both.TrySetResult();
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                    await UnauthorizedAsync(route);
                }
                else await route.ContinueAsync();
            });
            await NavigateAsync(Page, "/account/orders");
            await first.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await NavigateAsync(Page, $"/account/orders/{Guid.NewGuid():D}");
            await both.Task.WaitAsync(TimeSpan.FromSeconds(30));
            release.TrySetResult();
            await Expect(Page.GetByText("Order not found", new() { Exact = true })).ToBeVisibleAsync();
            Assert.Equal(1, refreshCount);
            await Page.EvaluateAsync("""
                async () => {
                    const module = await import('/js/authSessionSync.js');
                    for (let i = 0; i < 3; i++) {
                        module.publish('session-refreshed');
                        const value = localStorage.getItem('blazorshop.auth-session-event');
                        dispatchEvent(new StorageEvent('storage', { key: 'blazorshop.auth-session-event', newValue: value }));
                    }
                    dispatchEvent(new StorageEvent('storage', { key: 'blazorshop.auth-session-event', newValue: JSON.stringify({type:'signed-out',epoch:'obsolete'}) }));
                    await navigator.locks.request(module.lockName, () => {});
                }
                """);
            Assert.Equal(1, refreshCount);
            Assert.True(await Page.EvaluateAsync<bool>("!!sessionStorage.getItem('token')"));
        }
        finally { release.TrySetResult(); await application.RemoveScenarioAsync(data); }
    }

    [Fact]
    public async Task LogoutDuringCommittedRefresh_DiscardsLateResponseAndSurvivesReload()
    {
        var data = await application.SeedScenarioAsync();
        var release = Signal();
        try
        {
            await LoginAsync(Page, data);
            var other = await Context.NewPageAsync();
            await GoAsync(other, "account");
            await Expect(other.Locator("#account-content h1")).ToBeVisibleAsync();
            var committed = Signal();
            await Page.RouteAsync(RefreshPath, async route =>
            {
                var response = await route.FetchAsync();
                Assert.Equal(200, response.Status);
                committed.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                await route.FulfillAsync(new() { Response = response });
                await response.DisposeAsync();
            });
            await ForceOrders401Async(Page);
            await NavigateAsync(Page, "/account/orders");
            await committed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await NavigateAsync(other, "/authentication/logout");
            await other.WaitForFunctionAsync("JSON.parse(localStorage.getItem('blazorshop.auth-session-state')).status === 'signed-out'");
            release.TrySetResult();
            await Expect(other).ToHaveURLAsync(new Regex("/authentication/login/account$"));
            await Page.WaitForFunctionAsync("sessionStorage.getItem('token') === null");
            await Page.UnrouteAsync(RefreshPath);
            var refreshes = 0;
            await Context.RouteAsync(RefreshPath, async route => { Interlocked.Increment(ref refreshes); await route.ContinueAsync(); });
            await Page.ReloadAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Sign in", Exact = true })).ToBeVisibleAsync();
            Assert.Equal(0, refreshes);
            Assert.DoesNotContain(await Context.CookiesAsync(), cookie => cookie.Name == "__Host-blazorshop-refresh");
            await AssertMetadataOnlyAsync(other);
        }
        finally { release.TrySetResult(); await application.RemoveScenarioAsync(data); }
    }

    [Fact]
    public async Task LoginDuringRefresh_AccountSwitchWinsOverLateResponse()
    {
        var firstUser = await application.SeedScenarioAsync();
        var secondUser = await application.SeedScenarioAsync();
        var release = Signal();
        try
        {
            await LoginAsync(Page, firstUser);
            var other = await Context.NewPageAsync();
            await GoAsync(other, "account");
            await Expect(other.Locator("#account-content h1")).ToBeVisibleAsync();
            await other.EvaluateAsync("sessionStorage.removeItem('token')");
            await NavigateAsync(other, "/authentication/login");
            await Expect(other.GetByRole(AriaRole.Heading, new() { Name = "Sign in", Exact = true })).ToBeVisibleAsync();
            var reached = Signal();
            var once = 0;
            await Page.RouteAsync(RefreshPath, async route =>
            {
                if (Interlocked.Increment(ref once) == 1)
                {
                    reached.TrySetResult();
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                }
                await route.ContinueAsync();
            });
            await ForceOrders401Async(Page);
            await NavigateAsync(Page, "/account/orders");
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await FillLoginAsync(other, secondUser);
            await other.GetByRole(AriaRole.Button, new() { Name = "Login", Exact = true }).ClickAsync();
            await other.WaitForFunctionAsync("JSON.parse(localStorage.getItem('blazorshop.auth-session-state')).status === 'authenticating'");
            release.TrySetResult();
            await Expect(other).ToHaveURLAsync(new Regex("/account$"));
            await Page.WaitForFunctionAsync("email => { try { const token=sessionStorage.getItem('token'); return !!token && Object.values(JSON.parse(atob(token.split('.')[1].replace(/-/g,'+').replace(/_/g,'/')))).includes(email); } catch { return false; } }", secondUser.UserEmail);
            await NavigateAsync(Page, "/account/profile");
            await Expect(Page.GetByLabel("Email Address", new() { Exact = true })).ToHaveValueAsync(secondUser.UserEmail);
            await AssertMetadataOnlyAsync(Page);
        }
        finally { release.TrySetResult(); await application.RemoveScenarioAsync(firstUser); await application.RemoveScenarioAsync(secondUser); }
    }

    [Fact]
    public async Task LostHttpResponse_RequiresLoginAndDoesNotRetryAfterReload()
    {
        var data = await application.SeedScenarioAsync();
        try
        {
            await LoginAsync(Page, data);
            var lost = Signal();
            var refreshes = 0;
            await Page.RouteAsync(RefreshPath, async route =>
            {
                Interlocked.Increment(ref refreshes);
                var committed = await route.FetchAsync();
                Assert.Equal(200, committed.Status);
                await committed.DisposeAsync();
                await route.AbortAsync("failed");
                lost.TrySetResult();
            });
            await ForceOrders401Async(Page);
            await NavigateAsync(Page, "/account/orders");
            await lost.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await Page.WaitForFunctionAsync("JSON.parse(localStorage.getItem('blazorshop.auth-session-state')).status === 'signed-out'");
            await Page.ReloadAsync();
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Sign in", Exact = true })).ToBeVisibleAsync();
            Assert.Equal(1, refreshes);
        }
        finally { await application.RemoveScenarioAsync(data); }
    }

    [Fact]
    public async Task UnavailableWebLocks_FailsClosedWithoutCredentialPost()
    {
        var data = await application.SeedScenarioAsync();
        try
        {
            await LoginAsync(Page, data);
            var requests = 0;
            await Context.RouteAsync(RefreshPath, async route => { Interlocked.Increment(ref requests); await route.ContinueAsync(); });
            foreach (var mode in new[] { "missing", "rejected" })
            {
                await Page.EvaluateAsync("mode => Object.defineProperty(navigator, 'locks', { configurable:true, value: mode === 'missing' ? undefined : {request: () => Promise.reject(new Error('unavailable'))} })", mode);
                await Page.EvaluateAsync("sessionStorage.removeItem('token')");
                await NavigateAsync(Page, "/authentication/login");
                await FillLoginAsync(Page, data);
                await Page.GetByRole(AriaRole.Button, new() { Name = "Login", Exact = true }).ClickAsync();
                await Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("coordination is unavailable");
                Assert.True(await Page.EvaluateAsync<bool>("sessionStorage.getItem('token') === null"));
            }
            Assert.Equal(0, requests);
        }
        finally { await application.RemoveScenarioAsync(data); }
    }

    [Fact]
    public async Task LockTimeoutAndTabInterruption_ReleaseWithoutExecutingStaleCallback()
    {
        await GoAsync(Page, "authentication/login");
        await Expect(Page.Locator("h1")).ToBeVisibleAsync();
        var other = await Context.NewPageAsync();
        await GoAsync(other, "authentication/login");
        await Expect(other.Locator("h1")).ToBeVisibleAsync();
        await Page.EvaluateAsync("""
            async () => {
                const m = await import('/js/authSessionSync.js');
                localStorage.setItem('blazorshop.auth-session-state', JSON.stringify({epoch:'lock-test',status:'unknown'}));
                window.lockHeld = new Promise(held => {
                    window.holding = navigator.locks.request(m.lockName, () => new Promise(release => { window.releaseLock=release; held(); }));
                });
                await window.lockHeld;
            }
            """);
        var result = await other.EvaluateAsync<string>("""
            async () => {
                const m = await import('/js/authSessionSync.js');
                window.callbackCount=0;
                return m.runExclusive({invokeMethodAsync: async () => {window.callbackCount++;}}, 'cancelled', 'refresh', m.currentEpoch(), 20);
            }
            """);
        Assert.Equal("unavailable", result);
        Assert.Equal(0, await other.EvaluateAsync<int>("window.callbackCount"));
        // Closing the owning document releases its Web Lock; the next tab can proceed.
        await Page.CloseAsync();
        Assert.Equal("completed", await other.EvaluateAsync<string>("""
            async () => {
                const m=await import('/js/authSessionSync.js');
                return m.runExclusive({invokeMethodAsync:async()=>{window.callbackCount++;}}, 'next', 'refresh', m.currentEpoch());
            }
            """));
        Assert.Equal(1, await other.EvaluateAsync<int>("window.callbackCount"));
    }

    private async Task ForceOrders401Async(IPage page)
    {
        var sent = false;
        await page.RouteAsync("**/api/cart/user/orders", async route =>
        {
            if (!sent) { sent = true; await UnauthorizedAsync(route); }
            else await route.ContinueAsync();
        });
    }
    private Task UnauthorizedAsync(IRoute route) => route.FulfillAsync(new()
    {
        Status = 401, ContentType = "application/json", Body = "{}",
        Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = application.WebBaseUrl.GetLeftPart(UriPartial.Authority), ["Access-Control-Allow-Credentials"] = "true" },
    });
    private async Task LoginAsync(IPage page, E2ETestData data)
    {
        await GoAsync(page, "authentication/login");
        await FillLoginAsync(page, data);
        await page.GetByRole(AriaRole.Button, new() { Name = "Login", Exact = true }).ClickAsync();
        await Expect(page).ToHaveURLAsync(new Regex("/account$"));
    }
    private static async Task FillLoginAsync(IPage page, E2ETestData data)
    {
        await page.GetByLabel("Email Address", new() { Exact = true }).FillAsync(data.UserEmail);
        await page.GetByLabel("Password", new() { Exact = true }).FillAsync(data.UserPassword);
    }
    private Task GoAsync(IPage page, string path) => page.GotoAsync(new Uri(application.WebBaseUrl, path).ToString());
    private static Task NavigateAsync(IPage page, string path) => page.EvaluateAsync("path => Blazor.navigateTo(path)", path);
    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static async Task AssertMetadataOnlyAsync(IPage page) => Assert.True(await page.EvaluateAsync<bool>("""
        () => {
            const state=JSON.parse(localStorage.getItem('blazorshop.auth-session-state'));
            const event=JSON.parse(localStorage.getItem('blazorshop.auth-session-event'));
            return Object.keys(state).sort().join(',') === 'epoch,status' && Object.keys(event).sort().join(',') === 'epoch,nonce,type'
                && !document.cookie.includes('__Host-blazorshop-refresh=');
        }
        """));
    private async Task CaptureAsync(string state)
    {
        var path = Path.Combine(application.ArtifactDirectory, "refresh-coordination");
        Directory.CreateDirectory(path);
        await Page.ScreenshotAsync(new() { Path = Path.Combine(path, $"{state}.png"), FullPage = true });
    }
}
