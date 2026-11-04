namespace BlazorShop.E2E;

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

/// <summary>
/// Records a redacted, path-only browser timeline for diagnosing slow or stalled WebAssembly
/// boots (RC audit R01). It never records cookies, headers, request bodies, query strings,
/// storage values or token contents; console text is truncated and JWT-shaped values are redacted.
/// </summary>
internal sealed partial class BrowserBootDiagnostics
{
    private const int MaxEvents = 600;
    private const int MaxTextLength = 300;
    private const string RefreshSuffix = "/api/authentication/refresh-token";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly List<string> events = [];
    private readonly object gate = new();
    private int frameworkRequests;

    private BrowserBootDiagnostics(string label) => Label = label;

    public string Label { get; }

    public long ElapsedMs => clock.ElapsedMilliseconds;

    /// <summary>Start of the navigation being measured (0 for the tab's first navigation).</summary>
    public long NavigationStartMs { get; private set; }

    public long? LoadMs { get; private set; }

    public long? FirstRefreshRequestMs { get; private set; }

    public long? FirstRefreshResponseMs { get; private set; }

    public int? FirstRefreshStatus { get; private set; }

    public int FrameworkRequests => Volatile.Read(ref frameworkRequests);

    public IReadOnlyList<string> Events
    {
        get
        {
            lock (gate)
            {
                return events.ToArray();
            }
        }
    }

    public static BrowserBootDiagnostics Attach(IPage page, string label)
    {
        var recorder = new BrowserBootDiagnostics(label);
        page.Request += (_, request) => recorder.OnRequest(request);
        page.Response += (_, response) => recorder.OnResponse(response);
        page.RequestFailed += (_, request) => recorder.Add($"request-failed {request.Method} {PathOf(request.Url)} {Redact(request.Failure)}");
        page.Console += (_, message) => recorder.Add($"console {message.Type} {Redact(message.Text)}");
        page.PageError += (_, error) => recorder.Add($"page-error {Redact(error)}");
        page.DOMContentLoaded += (_, _) => recorder.Add("domcontentloaded");
        page.Load += (_, _) =>
        {
            recorder.LoadMs ??= recorder.ElapsedMs;
            recorder.Add("load");
        };
        page.Close += (_, _) => recorder.Add("close");
        return recorder;
    }

    /// <summary>
    /// Starts measuring a new navigation of the same tab (for example a reload). Load, refresh and
    /// framework-request metrics restart, so the next boot record is relative to this navigation.
    /// </summary>
    public void MarkNavigation(string description)
    {
        NavigationStartMs = ElapsedMs;
        LoadMs = null;
        FirstRefreshRequestMs = null;
        FirstRefreshResponseMs = null;
        FirstRefreshStatus = null;
        Interlocked.Exchange(ref frameworkRequests, 0);
        Add($"navigation {description}");
    }

    /// <summary>Converts a tab-clock value to milliseconds since the measured navigation started.</summary>
    public long? SinceNavigation(long? tabMs) => tabMs - NavigationStartMs;

    /// <summary>
    /// Installs an E2E-only probe for the tab's next documents. It records, relative to each new
    /// document's navigation start: DOMContentLoaded/load, the first Blazor render, the
    /// "Authorizing" and sign-in UI states, auth-session status/pending transitions, and the
    /// request/grant/release of Web Locks (session restoration). It never reads the epoch, JWT,
    /// cookies, headers or bodies; storage writes are reduced to status and the pending flag.
    /// </summary>
    public static Task InstallStartupProbeAsync(IPage page) => page.AddInitScriptAsync(StartupProbeScript);

    /// <summary>Reads the startup probe of the current document, or a short reason when unavailable.</summary>
    public static async Task<object?> ReadStartupProbeAsync(IPage page)
    {
        if (page.IsClosed)
        {
            return "closed";
        }

        try
        {
            return await page.EvaluateAsync<JsonElement>("() => window.__bsStartupProbe ? window.__bsStartupProbe.read() : null")
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception exception)
        {
            return $"unavailable: {exception.GetType().Name}";
        }
    }

    /// <summary>
    /// Reads boot/coordination metadata from a page. Only booleans, statuses, lock names and a
    /// short visible-text excerpt are returned; the epoch, JWT and cookies are never read out.
    /// </summary>
    public static async Task<object?> CaptureStateAsync(IPage page)
    {
        if (page.IsClosed)
        {
            return "closed";
        }

        try
        {
            return await page.EvaluateAsync<JsonElement>("""
                async () => {
                    let auth;
                    try {
                        const value = JSON.parse(localStorage.getItem('blazorshop.auth-session-state'));
                        auth = value ? { status: value.status ?? null, pending: value.pending === true } : null;
                    } catch { auth = 'unreadable'; }
                    let locks = 'unsupported';
                    if (navigator.locks?.query) {
                        const snapshot = await navigator.locks.query();
                        locks = {
                            held: snapshot.held.map(lock => `${lock.name}:${lock.mode}`),
                            pending: snapshot.pending.map(lock => `${lock.name}:${lock.mode}`),
                        };
                    }
                    const loader = document.querySelector('.loading-progress-text');
                    return {
                        url: location.pathname,
                        readyState: document.readyState,
                        visibility: document.visibilityState,
                        blazorDefined: typeof Blazor !== 'undefined',
                        loaderText: loader ? getComputedStyle(loader).getPropertyValue('--blazor-load-percentage-text').trim() : null,
                        errorUiVisible: getComputedStyle(document.getElementById('blazor-error-ui') ?? document.body).display !== 'none'
                            && !!document.getElementById('blazor-error-ui'),
                        hasTabToken: !!sessionStorage.getItem('token'),
                        auth,
                        locks,
                        visibleText: (document.body?.innerText ?? '').replace(/\s+/g, ' ').trim().slice(0, 400),
                    };
                }
                """).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception exception)
        {
            return $"unavailable: {exception.GetType().Name}";
        }
    }

    public static async Task WriteFailureAsync(string path, object report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, ToJson(report));
    }

    public static string ToJson(object report) => JsonSerializer.Serialize(report, JsonOptions);

    public object Summary() => new
    {
        label = Label,
        elapsedMs = ElapsedMs,
        navigationStartMs = NavigationStartMs,
        loadMs = LoadMs,
        firstRefreshRequestMs = FirstRefreshRequestMs,
        firstRefreshResponseMs = FirstRefreshResponseMs,
        firstRefreshStatus = FirstRefreshStatus,
        frameworkRequests = FrameworkRequests,
    };

    private void OnRequest(IRequest request)
    {
        var path = PathOf(request.Url);
        if (path.StartsWith("/_framework/", StringComparison.Ordinal))
        {
            if (Interlocked.Increment(ref frameworkRequests) == 1)
            {
                Add($"framework-first {path}");
            }

            return;
        }

        if (path.EndsWith(RefreshSuffix, StringComparison.Ordinal))
        {
            FirstRefreshRequestMs ??= ElapsedMs;
        }

        Add($"request {request.Method} {path}");
    }

    private void OnResponse(IResponse response)
    {
        var path = PathOf(response.Url);
        if (path.StartsWith("/_framework/", StringComparison.Ordinal))
        {
            return;
        }

        if (path.EndsWith(RefreshSuffix, StringComparison.Ordinal) && FirstRefreshResponseMs is null)
        {
            FirstRefreshResponseMs = ElapsedMs;
            FirstRefreshStatus = response.Status;
        }

        Add($"response {response.Status} {path}");
    }

    private void Add(string value)
    {
        lock (gate)
        {
            if (events.Count < MaxEvents)
            {
                events.Add($"{ElapsedMs,7} ms {value}");
            }
        }
    }

    private static string PathOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.AbsolutePath : "(non-absolute)";

    private static string Redact(string? value)
    {
        var redacted = JwtPattern().Replace(value ?? string.Empty, "[redacted-jwt]");
        return redacted.Length <= MaxTextLength ? redacted : redacted[..MaxTextLength] + "…";
    }

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*")]
    private static partial Regex JwtPattern();

    // Times are performance.now() of the new document, i.e. milliseconds since its navigation started.
    private const string StartupProbeScript = """
        (() => {
            if (window.__bsStartupProbe) return;
            const stateKey = 'blazorshop.auth-session-state';
            const lockPrefix = 'blazorshop.';
            const events = [];
            const add = (name, detail) => {
                if (events.length < 200) events.push(detail === undefined
                    ? { t: Math.round(performance.now()), name }
                    : { t: Math.round(performance.now()), name, detail });
            };
            const authOf = value => {
                try {
                    const parsed = JSON.parse(value);
                    return parsed ? { status: parsed.status ?? null, pending: parsed.pending === true } : null;
                } catch { return 'unreadable'; }
            };
            add('document-start', { auth: authOf(localStorage.getItem(stateKey)) });
            document.addEventListener('DOMContentLoaded', () => add('domcontentloaded'), { once: true });
            window.addEventListener('load', () => add('load'), { once: true });

            const setItem = Storage.prototype.setItem;
            const removeItem = Storage.prototype.removeItem;
            Storage.prototype.setItem = function (key, value) {
                const result = setItem.apply(this, arguments);
                if (this === localStorage && key === stateKey) add('auth-state', authOf(value));
                if (this === sessionStorage && key === 'token') add('tab-token-stored');
                return result;
            };
            Storage.prototype.removeItem = function (key) {
                const result = removeItem.apply(this, arguments);
                if (this === sessionStorage && key === 'token') add('tab-token-removed');
                return result;
            };

            if (typeof LockManager !== 'undefined' && LockManager.prototype.request) {
                const request = LockManager.prototype.request;
                LockManager.prototype.request = function (name, ...rest) {
                    const callback = rest.pop();
                    if (typeof name !== 'string' || !name.startsWith(lockPrefix) || typeof callback !== 'function') {
                        return request.call(this, name, ...rest, callback);
                    }
                    const label = `${name}:${rest[0]?.mode ?? 'exclusive'}`;
                    add('lock-request', label);
                    const outcome = request.call(this, name, ...rest, lock => {
                        add('lock-granted', label);
                        const result = callback(lock);
                        Promise.resolve(result).then(() => add('lock-released', label), () => add('lock-released', `${label} rejected`));
                        return result;
                    });
                    outcome.then(() => add('lock-request-settled', label), error => add('lock-request-settled', `${label} ${error?.name ?? 'error'}`));
                    return outcome;
                };
            }

            const seen = { render: false, blazor: false, authorizing: false, signInRequired: false, signIn: false };
            const flag = (key, value, shown, hidden) => {
                if (seen[key] !== value) { seen[key] = value; if (value || hidden) add(value ? shown : hidden); }
            };
            const check = () => {
                const app = document.getElementById('app');
                flag('render', seen.render || (!!app && !app.querySelector('.loader_main')), 'blazor-first-render');
                flag('blazor', seen.blazor || typeof Blazor !== 'undefined', 'blazor-defined');
                const text = document.body ? document.body.textContent : '';
                flag('authorizing', text.includes('Authorizing, please wait...'), 'authorizing-shown', 'authorizing-hidden');
                flag('signInRequired', text.includes('Sign-in required'), 'sign-in-required-shown', 'sign-in-required-hidden');
                flag('signIn', Array.from(document.querySelectorAll('h1')).some(h => h.textContent.trim() === 'Sign in'), 'sign-in-heading-shown', 'sign-in-heading-hidden');
            };
            new MutationObserver(check).observe(document, { childList: true, subtree: true, characterData: true });

            const resourcePattern = /blazor\.webassembly\.js$|\/dotnet[^/]*\.(js|wasm)$|authSessionSync\.js$|\/api\/authentication\/refresh-token$/;
            window.__bsStartupProbe = {
                read: () => {
                    const navigation = performance.getEntriesByType('navigation')[0];
                    const resources = performance.getEntriesByType('resource')
                        .map(entry => ({ path: new URL(entry.name).pathname, start: Math.round(entry.startTime), end: Math.round(entry.responseEnd) }))
                        .filter(entry => resourcePattern.test(entry.path));
                    return {
                        timeOrigin: Math.round(performance.timeOrigin),
                        now: Math.round(performance.now()),
                        domContentLoaded: navigation ? Math.round(navigation.domContentLoadedEventEnd) : null,
                        load: navigation ? Math.round(navigation.loadEventEnd) : null,
                        refreshRequests: resources.filter(entry => entry.path.endsWith('/refresh-token')).length,
                        resources,
                        events: events.slice(),
                    };
                },
            };
        })();
        """;
}

/// <summary>
/// A boot readiness wait failed. The message carries the redacted diagnostics; the original
/// Playwright failure is preserved as the inner exception.
/// </summary>
internal sealed class BootReadinessException(string message, Exception innerException) : Exception(message, innerException);

/// <summary>
/// A functional assertion failed after its readiness stage passed. The message carries the same
/// redacted diagnostics; the original assertion failure is preserved as the inner exception.
/// </summary>
internal sealed class ScenarioAssertionException(string message, Exception innerException) : Exception(message, innerException);
