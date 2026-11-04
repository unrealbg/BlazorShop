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
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, JsonOptions));
    }

    public object Summary() => new
    {
        label = Label,
        elapsedMs = ElapsedMs,
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
}
