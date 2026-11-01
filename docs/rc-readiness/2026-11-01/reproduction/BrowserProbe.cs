namespace BlazorShop.RcAudit;

using System.Text.Json;
using BlazorShop.E2E;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Xunit;

[CollectionDefinition("RC browser", DisableParallelization = true)]
public sealed class BrowserCollection : ICollectionFixture<E2EApplicationFixture> { }

[Collection("RC browser")]
public sealed class BrowserProbe(E2EApplicationFixture app) : PageTest
{
    public override BrowserNewContextOptions ContextOptions() => new() { IgnoreHTTPSErrors = true, Locale = "en-US" };

    [Fact]
    public async Task MeasureFunctionalUi_AccessibilityAndPerformance()
    {
        var data = await app.SeedScenarioAsync(includeListingSamples: true);
        var results = new List<object>();
        var axePath = Path.Combine(AuditPaths.Root, "axe", "package", "axe.min.js");
        var directory = Path.Combine(AuditPaths.Root, "screenshots");
        Directory.CreateDirectory(directory);
        var errors = new List<string>();
        Page.Console += (_, message) => { if (message.Type == "error") errors.Add(message.Text); };
        await Context.AddInitScriptAsync("""
            window.auditMetrics={lcp:0,cls:0};
            new PerformanceObserver(l=>{for(const e of l.getEntries())window.auditMetrics.lcp=e.startTime;}).observe({type:'largest-contentful-paint',buffered:true});
            new PerformanceObserver(l=>{for(const e of l.getEntries())if(!e.hadRecentInput)window.auditMetrics.cls+=e.value;}).observe({type:'layout-shift',buffered:true});
            """);
        try
        {
            foreach (var width in new[] { 320, 390, 768, 1440 })
            {
                await Page.SetViewportSizeAsync(width, 1000);
                foreach (var route in new[] { "", $"catalog?category={data.CategoryId}", $"product/{data.SimpleProductSlug}", "my-cart" })
                {
                    var label = route.StartsWith("catalog") ? "catalog" : route.StartsWith("product/") ? "product" : route == "" ? "home" : "cart-empty";
                    var response = await Page.GotoAsync(new Uri(app.StorefrontBaseUrl, route).ToString());
                    Assert.NotNull(response); Assert.Equal(200, response.Status);
                    await Expect(Page.Locator("main h1")).ToBeVisibleAsync();
                    await Page.WaitForTimeoutAsync(400);
                    await ScanAsync(label, width, response.Headers, results, axePath);
                    await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, $"{label}-{width}.png"), FullPage = true });
                }
            }
            await Page.SetViewportSizeAsync(390, 1000);
            var accessResponse = await Page.GotoAsync(new Uri(app.WebBaseUrl, "authentication/login").ToString());
            await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Sign in", Exact = true })).ToBeVisibleAsync(new() { Timeout = 30000 });
            await ScanAsync("wasm-login", 390, accessResponse!.Headers, results, axePath);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login", Exact = true }).ClickAsync();
            await ScanAsync("wasm-login-invalid", 390, accessResponse.Headers, results, axePath);
            await Page.ScreenshotAsync(new() { Path = Path.Combine(directory, "login-invalid-390.png"), FullPage = true });
            await Page.GetByLabel("Email Address", new() { Exact = true }).FillAsync(data.UserEmail);
            await Page.GetByLabel("Password", new() { Exact = true }).FillAsync(data.UserPassword);
            await Page.GetByRole(AriaRole.Button, new() { Name = "Login", Exact = true }).ClickAsync();
            await Expect(Page.Locator("#account-content h1")).ToBeVisibleAsync(new() { Timeout = 30000 });
            foreach (var route in new[] { "account", "account/profile", "account/settings", "account/orders", "account/notifications" })
            {
                var response = await Page.GotoAsync(new Uri(app.WebBaseUrl, route).ToString());
                await Expect(Page.Locator("#account-content h1")).ToBeVisibleAsync(new() { Timeout = 30000 });
                await ScanAsync("wasm-" + route.Replace('/', '-'), 390, response!.Headers, results, axePath);
            }
            await File.WriteAllTextAsync(Path.Combine(AuditPaths.Root, "ui-measurements.json"), JsonSerializer.Serialize(new { axeVersion = "4.14.0", mode = "Release binaries, Development Aspire hosting, warm loopback, no throttling", scans = results, consoleErrors = errors.Distinct().ToArray() }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { await app.RemoveScenarioAsync(data); }
    }

    private async Task ScanAsync(string label, int width, IReadOnlyDictionary<string, string> headers, List<object> results, string axePath)
    {
        var metrics = await Page.EvaluateAsync<JsonElement>("""
            () => {
              const n=performance.getEntriesByType('navigation')[0], r=performance.getEntriesByType('resource');
              return {ttfbMs:n?.responseStart,domContentLoadedMs:n?.domContentLoadedEventEnd,lcpMs:window.auditMetrics?.lcp,cls:window.auditMetrics?.cls,
                resourceCount:r.length,encodedBodyBytes:r.reduce((v,e)=>v+e.encodedBodySize,0),transferBytes:r.reduce((v,e)=>v+e.transferSize,0),
                overflow:document.documentElement.scrollWidth>innerWidth,images:[...document.querySelectorAll('main img')].map(e=>({lazy:e.loading==='lazy',hasSrcset:!!e.srcset,complete:e.complete,naturalWidth:e.naturalWidth,cssWidth:e.getBoundingClientRect().width}))};
            }
            """);
        await Page.EvaluateAsync(await File.ReadAllTextAsync(axePath));
        var audit = await Page.EvaluateAsync<JsonElement>("""
            async()=>{const r=await axe.run(document,{runOnly:{type:'tag',values:['wcag2a','wcag2aa','wcag21aa','wcag22aa']}});
              return {violations:r.violations.map(v=>({id:v.id,impact:v.impact,description:v.description,helpUrl:v.helpUrl,nodes:v.nodes.map(n=>({target:n.target,html:n.html,failureSummary:n.failureSummary}))})),incomplete:r.incomplete.map(v=>({id:v.id,nodes:v.nodes.length}))};}
            """);
        results.Add(new { label, width, metrics, headers = headers.Where(pair => new[] { "content-security-policy", "x-frame-options", "x-content-type-options", "cache-control", "x-robots-tag" }.Contains(pair.Key)).ToDictionary(), audit });
    }
}
