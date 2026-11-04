namespace BlazorShop.E2E;

using Microsoft.Playwright;

// EXPERIMENTS ONLY (branch codex/e2e-navigation-experiments-do-not-merge). Each experiment delays one
// resource of the checkout login page by 35 s with an E2E-only route, so the unchanged 30-second
// navigation timeout fires, and checks that the failure has the CI signature. Never merge.
public sealed partial class CheckoutPaymentTests
{
    private static readonly TimeSpan StallDelay = TimeSpan.FromSeconds(35);

    [Fact]
    public Task Experiment_StalledCdnjsStylesheet() => ExpectStalledNavigationAsync("https://cdnjs.cloudflare.com/**");

    [Fact]
    public Task Experiment_StalledJsdelivrScript() => ExpectStalledNavigationAsync("https://cdn.jsdelivr.net/**");

    [Fact]
    public Task Experiment_StalledLocalStylesheet() => ExpectStalledNavigationAsync("**/css/site.css*");

    [Fact]
    public Task Experiment_StalledLocalBlazorScript() => ExpectStalledNavigationAsync("**/_framework/blazor.webassembly.js");

    [Fact]
    public async Task Experiment_NoStall_RemovedAssetsAreNotRequested()
    {
        var notFound = new List<string>();
        var web = application.WebBaseUrl.GetLeftPart(UriPartial.Authority);
        Page.Response += (_, response) => { if (response.Status >= 400 && response.Url.StartsWith(web, StringComparison.Ordinal)) lock (notFound) notFound.Add($"{response.Status} {new Uri(response.Url).AbsolutePath}"); };
        await GotoCheckoutLoginAsync();
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Sign in", Exact = true })).ToBeVisibleAsync();
        Assert.Empty(notFound);
    }

    private async Task ExpectStalledNavigationAsync(string pattern)
    {
        await Page.RouteAsync(pattern, async route => { await Task.Delay(StallDelay); await route.ContinueAsync(); });
        var failure = await Assert.ThrowsAsync<NavigationTimeoutException>(GotoCheckoutLoginAsync);
        Assert.IsType<TimeoutException>(failure.InnerException);
        Assert.Contains("waiting until \"load\"", failure.InnerException!.Message);
        await Page.UnrouteAllAsync(new() { Behavior = UnrouteBehavior.IgnoreErrors });
    }
}
