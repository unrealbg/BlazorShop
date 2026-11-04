namespace BlazorShop.E2E;

using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;

[Collection(E2ECollection.Name)]
public sealed class RefreshCoordinationTests(E2EApplicationFixture application) : PageTest
{
    private const string RefreshPath = "**/api/authentication/refresh-token";

    // Boot readiness (WebAssembly start, session restoration and first rendered UI) is recorded per tab
    // and, when a wait fails, dumped as a redacted timeline for every tab (RC audit R01, issue #134).
    // Timeouts are deliberately unchanged: diagnostics must not turn a stall into a pass.
    private readonly Dictionary<IPage, BrowserBootDiagnostics> bootRecorders = new();

    // Wall-clock (Unix ms) of ReloadAsync() being called and returning, to place it on the reloaded
    // document's timeline (probe timeOrigin) without relying on the tab-creation clock.
    private readonly Dictionary<IPage, (long Called, long Returned)> reloadClock = new();

    // Cold boot of an additional tab gets the same 30-second budget that the first tab's login
    // navigation already has through Playwright's default action timeout. Firefox CI measured the
    // first rendered account UI at 5.24 s after opening the tab, past the 5-second assertion default,
    // with the refresh POST done in 94 ms and no Web Lock held or pending (issue #134). Only this
    // readiness wait uses the budget; every functional assertion keeps its existing timeout.
    private static readonly TimeSpan SecondaryTabBootReadinessTimeout = TimeSpan.FromSeconds(30);

    // Playwright's default action timeout, which this class does not override; recorded for the login wait.
    private static readonly TimeSpan DefaultActionTimeout = TimeSpan.FromSeconds(30);
    public override BrowserNewContextOptions ContextOptions() => new() { IgnoreHTTPSErrors = true, Locale = "en-US" };

    [Fact]
    public async Task TwoTabs_ConcurrentBootstrapSerializesCookieRotation()
    {
        var data = await application.SeedScenarioAsync();
        var release = Signal();
        try
        {
            await LoginAsync(Page, data);
            var other = await OpenSecondaryTabAsync("account", page => page.Locator("#account-content h1"));
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
            var other = await OpenSecondaryTabAsync("account", page => page.Locator("#account-content h1"));
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
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "No orders yet", Exact = true })).ToBeVisibleAsync();
            Assert.Equal(1, refreshCount);
            await Page.EvaluateAsync("""
                async () => {
                    const module = await import('/js/authSessionSync.js');
                    module.publish('signed-in');
                    const value = localStorage.getItem('blazorshop.auth-session-event');
                    for (let i = 0; i < 3; i++) {
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
    public async Task ProfileNotification_UpdatesPeerClaimsAndDeduplicatesTheSameEvent()
    {
        var data = await application.SeedScenarioAsync();
        try
        {
            await LoginAsync(Page, data);
            var other = await OpenSecondaryTabAsync("account/profile", page => page.GetByLabel("Full Name", new() { Exact = true }));
            await NavigateAsync(Page, "/account/profile");
            await Expect(Page.GetByLabel("Full Name", new() { Exact = true })).ToBeVisibleAsync();
            var refreshes = 0;
            await Context.RouteAsync(RefreshPath, async route => { Interlocked.Increment(ref refreshes); await route.ContinueAsync(); });
            const string updatedName = "Cross-tab Updated Customer";
            await Page.GetByLabel("Full Name", new() { Exact = true }).FillAsync(updatedName);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Save changes", Exact = true }).ClickAsync();
            await other.WaitForFunctionAsync("name => { try { return Object.values(JSON.parse(atob(sessionStorage.getItem('token').split('.')[1].replace(/-/g,'+').replace(/_/g,'/')))).includes(name); } catch { return false; } }", updatedName);
            Assert.Equal(2, refreshes);
            await other.EvaluateAsync("""
                async () => {
                    const m = await import('/js/authSessionSync.js');
                    const value = localStorage.getItem('blazorshop.auth-session-event');
                    for (let i=0;i<3;i++) dispatchEvent(new StorageEvent('storage', {key:'blazorshop.auth-session-event',newValue:value}));
                    await navigator.locks.request(m.lockName, () => {});
                }
                """);
            Assert.Equal(2, refreshes);
            await NavigateAsync(other, "/account");
            await NavigateAsync(other, "/account/profile");
            await Expect(other.GetByLabel("Full Name", new() { Exact = true })).ToHaveValueAsync(updatedName);
        }
        finally { await application.RemoveScenarioAsync(data); }
    }

    [Fact]
    public async Task LogoutDuringCommittedRefresh_DiscardsLateResponseAndSurvivesReload()
    {
        var data = await application.SeedScenarioAsync();
        var release = Signal();
        try
        {
            await LoginAsync(Page, data);
            var other = await OpenSecondaryTabAsync("account", page => page.Locator("#account-content h1"));
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
            var other = await OpenSecondaryTabAsync("account", page => page.Locator("#account-content h1"));
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
    public async Task TabClosesWithUncertainRefresh_AnotherTabRequiresLoginWithoutPost()
    {
        var data = await application.SeedScenarioAsync();
        var release = Signal();
        try
        {
            await LoginAsync(Page, data);
            var other = await OpenSecondaryTabAsync("account", page => page.Locator("#account-content h1"));
            var committed = Signal();
            await Page.RouteAsync(RefreshPath, async route =>
            {
                var response = await route.FetchAsync();
                Assert.Equal(200, response.Status);
                committed.TrySetResult();
                await release.Task.WaitAsync(TimeSpan.FromSeconds(30));
                try { await route.FulfillAsync(new() { Response = response }); }
                catch (PlaywrightException) when (Page.IsClosed) { }
                await response.DisposeAsync();
            });
            await ForceOrders401Async(Page);
            await NavigateAsync(Page, "/account/orders");
            await committed.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await other.WaitForFunctionAsync("JSON.parse(localStorage.getItem('blazorshop.auth-session-state')).pending === true");
            await Page.CloseAsync();
            release.TrySetResult();
            var attempted = 0;
            await other.RouteAsync(RefreshPath, async route => { Interlocked.Increment(ref attempted); await route.ContinueAsync(); });
            await ReloadWithStartupProbeAsync(other, "secondary reload");
            await AssertWithDiagnosticsAsync(other, "sign-in after reload", async () =>
            {
                await Expect(other.GetByRole(AriaRole.Heading, new() { Name = "Sign in", Exact = true })).ToBeVisibleAsync();
                Assert.Equal(0, attempted);
                await AssertMetadataOnlyAsync(other);
            });
        }
        finally { release.TrySetResult(); await application.RemoveScenarioAsync(data); }
    }

    [Fact]
    public async Task RefreshRedirect_IsRejectedWithoutRepostingCookie()
    {
        var data = await application.SeedScenarioAsync();
        try
        {
            await LoginAsync(Page, data);
            var repeated = 0;
            await Page.RouteAsync("**/api/authentication/refresh-token?*", async route =>
            {
                Interlocked.Increment(ref repeated);
                await route.FulfillAsync(new() { Status = 400, ContentType = "application/json", Body = "{}" });
            });
            await Page.RouteAsync(RefreshPath, route => route.FulfillAsync(new()
            {
                Status = 307, Headers = new Dictionary<string, string>
                {
                    ["Location"] = new Uri(application.ApiBaseUrl, "api/authentication/refresh-token?retry=1").ToString(),
                    ["Access-Control-Allow-Origin"] = application.WebBaseUrl.GetLeftPart(UriPartial.Authority),
                    ["Access-Control-Allow-Credentials"] = "true",
                },
            }));
            await ForceOrders401Async(Page);
            await NavigateAsync(Page, "/account/orders");
            await Page.WaitForFunctionAsync("JSON.parse(localStorage.getItem('blazorshop.auth-session-state')).status === 'signed-out'");
            Assert.Equal(0, repeated);
            Assert.True(await Page.EvaluateAsync<bool>("sessionStorage.getItem('token') === null"));
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
            var loginRequests = 0;
            await Context.RouteAsync(RefreshPath, async route => { Interlocked.Increment(ref requests); await route.ContinueAsync(); });
            await Context.RouteAsync("**/api/authentication/login", async route => { Interlocked.Increment(ref loginRequests); await route.ContinueAsync(); });
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
            Assert.Equal(0, loginRequests);
        }
        finally { await application.RemoveScenarioAsync(data); }
    }

    [Fact]
    public async Task LockTimeoutAndTabInterruption_ReleaseWithoutExecutingStaleCallback()
    {
        await GoAsync(Page, "authentication/login");
        await Expect(Page.Locator("h1")).ToBeVisibleAsync();
        var other = await OpenSecondaryTabAsync("authentication/login", page => page.GetByRole(AriaRole.Heading, new() { Name = "Sign in", Exact = true }));
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
    private async Task<IPage> OpenSecondaryTabAsync(string path, Func<IPage, ILocator> ready, [CallerMemberName] string scenario = "")
    {
        TrackBoot(Page, "primary");
        var other = await Context.NewPageAsync();
        TrackBoot(other, "secondary");
        // Readiness only: a page-specific element rendered by Blazor, never window.load or the loader text.
        await WaitForBootAsync(other, $"secondary {path}", async () =>
        {
            await GoAsync(other, path);
            await Expect(ready(other)).ToBeVisibleAsync(new() { Timeout = (float)SecondaryTabBootReadinessTimeout.TotalMilliseconds });
        }, scenario, SecondaryTabBootReadinessTimeout);
        return other;
    }

    private BrowserBootDiagnostics TrackBoot(IPage page, string label)
    {
        if (!bootRecorders.TryGetValue(page, out var recorder))
        {
            recorder = BrowserBootDiagnostics.Attach(page, label);
            bootRecorders[page] = recorder;
        }

        return recorder;
    }

    private async Task WaitForBootAsync(IPage page, string step, Func<Task> wait, string scenario, TimeSpan budget)
    {
        var recorder = bootRecorders[page];
        var directory = Path.Combine(application.ArtifactDirectory, "refresh-coordination");
        var browser = Browser.BrowserType.Name;
        try
        {
            await wait();
            await AppendBootRecordAsync(page, directory, browser, scenario, step, recorder, budget, ready: true);
        }
        catch (Exception exception)
        {
            await AppendBootRecordAsync(page, directory, browser, scenario, step, recorder, budget, ready: false);
            var report = await WriteFailureReportAsync("boot-failure", page, scenario, step, (long)budget.TotalMilliseconds);

            // Also surface the redacted report in the test result itself, so it is readable from CI logs
            // even when artifacts are unavailable. The original failure stays the inner exception.
            throw new BootReadinessException(
                $"Boot wait '{step}' failed for the {recorder.Label} tab ({browser}). Redacted boot diagnostics:{Environment.NewLine}"
                + BrowserBootDiagnostics.ToJson(report),
                exception);
        }
    }

    // Functional assertions keep their own timeouts; on failure the same redacted report is kept so an
    // assertion failure after a successful boot is diagnosable too (issue #134).
    private async Task AssertWithDiagnosticsAsync(IPage page, string step, Func<Task> assertions, [CallerMemberName] string scenario = "")
    {
        try
        {
            await assertions();
            await AppendStartupRecordAsync(page, scenario, step, passed: true);
        }
        catch (Exception exception)
        {
            await AppendStartupRecordAsync(page, scenario, step, passed: false);
            var report = await WriteFailureReportAsync("assertion-failure", page, scenario, step, budgetMs: null);
            throw new ScenarioAssertionException(
                $"Assertion step '{step}' failed for the {bootRecorders[page].Label} tab ({Browser.BrowserType.Name}). Redacted diagnostics:{Environment.NewLine}"
                + BrowserBootDiagnostics.ToJson(report),
                exception);
        }
    }

    // One line per probed assertion step in refresh-coordination/startup-timeline.jsonl, so passing runs
    // also show when the reloaded document rendered, restored its session and reached the expected UI.
    private async Task AppendStartupRecordAsync(IPage page, string scenario, string step, bool passed)
    {
        var recorder = bootRecorders[page];
        if (recorder.NavigationStartMs == 0)
        {
            return;
        }

        var directory = Path.Combine(application.ArtifactDirectory, "refresh-coordination");
        Directory.CreateDirectory(directory);
        var record = System.Text.Json.JsonSerializer.Serialize(new
        {
            browser = Browser.BrowserType.Name,
            scenario,
            step,
            tab = recorder.Label,
            passed,
            elapsedSinceNavigationMs = recorder.SinceNavigation(recorder.ElapsedMs),
            loadMs = recorder.SinceNavigation(recorder.LoadMs),
            frameworkRequests = recorder.FrameworkRequests,
            reloadCalledUnixMs = reloadClock.TryGetValue(page, out var clock) ? clock.Called : (long?)null,
            reloadReturnedUnixMs = reloadClock.TryGetValue(page, out var returned) ? returned.Returned : (long?)null,
            startup = await BrowserBootDiagnostics.ReadStartupProbeAsync(page),
        });
        await File.AppendAllTextAsync(Path.Combine(directory, "startup-timeline.jsonl"), record + Environment.NewLine);
    }

    // Reloads a tab as a new measured navigation: tab metrics restart and the startup probe records the
    // new document relative to its own navigation start, not to the creation of the tab.
    private async Task ReloadWithStartupProbeAsync(IPage page, string description)
    {
        await BrowserBootDiagnostics.InstallStartupProbeAsync(page);
        bootRecorders[page].MarkNavigation(description);
        var called = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await page.ReloadAsync();
        reloadClock[page] = (called, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    private async Task<object> WriteFailureReportAsync(string kind, IPage failedPage, string scenario, string step, long? budgetMs)
    {
        var directory = Path.Combine(application.ArtifactDirectory, "refresh-coordination");
        var browser = Browser.BrowserType.Name;
        var pages = new List<object>();
        foreach (var (trackedPage, tracked) in bootRecorders)
        {
            pages.Add(new
            {
                summary = tracked.Summary(),
                state = await BrowserBootDiagnostics.CaptureStateAsync(trackedPage),
                startup = await BrowserBootDiagnostics.ReadStartupProbeAsync(trackedPage),
                events = tracked.Events,
            });
        }

        var report = new { kind, browser, scenario, step, budgetMs, failedTab = bootRecorders[failedPage].Label, pages };
        var fileStep = new string(step.Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());
        await BrowserBootDiagnostics.WriteFailureAsync(Path.Combine(directory, $"{kind}-{browser}-{scenario}-{fileStep}.json"), report);
        return report;
    }

    private static async Task AppendBootRecordAsync(IPage page, string directory, string browser, string scenario, string step, BrowserBootDiagnostics recorder, TimeSpan budget, bool ready)
    {
        Directory.CreateDirectory(directory);
        // Times are relative to the measured navigation (the tab's first navigation, or a later reload).
        var record = System.Text.Json.JsonSerializer.Serialize(new
        {
            browser,
            scenario,
            step,
            tab = recorder.Label,
            budgetMs = (long)budget.TotalMilliseconds,
            ready,
            readyMs = ready ? recorder.SinceNavigation(recorder.ElapsedMs) : null,
            loadMs = recorder.SinceNavigation(recorder.LoadMs),
            firstRefreshRequestMs = recorder.SinceNavigation(recorder.FirstRefreshRequestMs),
            firstRefreshResponseMs = recorder.SinceNavigation(recorder.FirstRefreshResponseMs),
            firstRefreshStatus = recorder.FirstRefreshStatus,
            frameworkRequests = recorder.FrameworkRequests,
            startup = recorder.NavigationStartMs > 0 ? await BrowserBootDiagnostics.ReadStartupProbeAsync(page) : null,
        });
        await File.AppendAllTextAsync(Path.Combine(directory, "boot-timeline.jsonl"), record + Environment.NewLine);
    }

    private async Task LoginAsync(IPage page, E2ETestData data, [CallerMemberName] string scenario = "")
    {
        TrackBoot(page, page == Page ? "primary" : "secondary");
        // WaitForAsync uses the same default action timeout that FillAsync applied implicitly before.
        await WaitForBootAsync(page, "login form", async () =>
        {
            await GoAsync(page, "authentication/login");
            await page.GetByLabel("Email Address", new() { Exact = true }).WaitForAsync();
        }, scenario, DefaultActionTimeout);
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
