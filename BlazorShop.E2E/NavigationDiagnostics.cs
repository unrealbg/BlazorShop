namespace BlazorShop.E2E;

using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

/// <summary>
/// E2E-only diagnostics for a page navigation that does not reach its load state. On timeout it records,
/// relative to the start of the navigation: every request still pending (origin, path, resource type,
/// elapsed time), failed and slow requests, navigation milestones, document readiness, the WebAssembly
/// bootstrap state, browser errors, an out-of-browser probe of the given servers and the runner load.
/// It never records query strings, fragments, headers, cookies, request or response bodies, tokens or
/// storage values; recorded text is truncated, URL queries/fragments are dropped and JWT- and
/// e-mail-shaped values are redacted. The probes run only after a timeout and are bounded in time.
/// </summary>
internal sealed partial class NavigationDiagnostics
{
    private const int MaxEvents = 300;
    private const int MaxTextLength = 200;
    private const long SlowRequestMs = 2000;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly object gate = new();
    private readonly Dictionary<IRequest, RequestEntry> requests = new();
    private readonly List<string> events = [];

    private NavigationDiagnostics()
    {
    }

    public static NavigationDiagnostics Attach(IPage page)
    {
        var diagnostics = new NavigationDiagnostics();
        page.Request += (_, request) => diagnostics.OnRequest(request);
        page.Response += (_, response) => diagnostics.OnResponse(response);
        page.RequestFinished += (_, request) => diagnostics.OnFinished(request, failure: null);
        page.RequestFailed += (_, request) => diagnostics.OnFinished(request, Redact(request.Failure));
        page.DOMContentLoaded += (_, _) => diagnostics.Add("domcontentloaded");
        page.Load += (_, _) => diagnostics.Add("load");
        page.FrameNavigated += (_, frame) =>
        {
            if (frame == page.MainFrame)
            {
                diagnostics.Add($"main-frame-navigated {PathOf(frame.Url)}");
            }
        };
        page.Console += (_, message) =>
        {
            if (message.Type is "error" or "warning")
            {
                diagnostics.Add($"console {message.Type} {Redact(message.Text)}");
            }
        };
        page.PageError += (_, error) => diagnostics.Add($"page-error {Redact(error)}");
        return diagnostics;
    }

    /// <summary>
    /// Navigates exactly like <see cref="IPage.GotoAsync"/> (same wait state and timeout). When the navigation
    /// times out, a redacted report is written to <paramref name="artifactDirectory"/> and surfaced in the
    /// exception message; the original Playwright exception is preserved as the inner exception.
    /// </summary>
    public async Task<IResponse?> GotoAsync(IPage page, string url, string step, string artifactDirectory, IReadOnlyList<Uri> probes)
    {
        long start;
        lock (gate)
        {
            start = clock.ElapsedMilliseconds;
            events.Clear();
            foreach (var request in requests.Keys.Where(request => requests[request].EndMs is not null).ToList())
            {
                requests.Remove(request);
            }
        }

        Add($"goto {PathOf(url)}");
        try
        {
            return await page.GotoAsync(url);
        }
        catch (TimeoutException exception)
        {
            var report = await CaptureAsync(page, step, start, probes);
            var directory = Path.Combine(artifactDirectory, "navigation-diagnostics");
            Directory.CreateDirectory(directory);
            var fileStep = new string(step.Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray());
            var json = JsonSerializer.Serialize(report, JsonOptions);
            await File.WriteAllTextAsync(Path.Combine(directory, $"navigation-timeout-{fileStep}-{DateTime.UtcNow:HHmmss}.json"), json);
            throw new NavigationTimeoutException(
                $"Navigation '{step}' did not reach its load state. Redacted navigation diagnostics:{Environment.NewLine}{json}",
                exception);
        }
    }

    private async Task<object> CaptureAsync(IPage page, string step, long start, IReadOnlyList<Uri> probes)
    {
        var now = clock.ElapsedMilliseconds;
        List<object> pending, failed, slow;
        object byOrigin;
        string[] timeline;
        lock (gate)
        {
            var entries = requests.Values.Where(entry => entry.StartMs >= start).ToList();
            pending = entries.Where(entry => entry.EndMs is null)
                .OrderBy(entry => entry.StartMs)
                .Select(entry => (object)new { entry.Origin, entry.Path, entry.ResourceType, entry.Method, startedMs = entry.StartMs - start, pendingForMs = now - entry.StartMs, entry.Status })
                .ToList();
            failed = entries.Where(entry => entry.Failure is not null)
                .Select(entry => (object)new { entry.Origin, entry.Path, entry.ResourceType, startedMs = entry.StartMs - start, durationMs = entry.EndMs - entry.StartMs, entry.Failure })
                .ToList();
            slow = entries.Where(entry => entry.Failure is null && entry.EndMs - entry.StartMs >= SlowRequestMs)
                .Select(entry => (object)new { entry.Origin, entry.Path, entry.ResourceType, startedMs = entry.StartMs - start, durationMs = entry.EndMs - entry.StartMs, entry.Status })
                .ToList();
            byOrigin = entries.GroupBy(entry => entry.Origin).ToDictionary(group => group.Key, group => new
            {
                total = group.Count(),
                pending = group.Count(entry => entry.EndMs is null),
                failed = group.Count(entry => entry.Failure is not null),
            });
            timeline = events.ToArray();
        }

        return new
        {
            step,
            elapsedMs = now - start,
            pendingRequests = pending,
            failedRequests = failed,
            slowRequests = slow,
            requestsByOrigin = byOrigin,
            document = await ReadDocumentStateAsync(page),
            serverProbes = await ProbeAsync(probes),
            runner = ReadRunnerLoad(),
            timeline,
        };
    }

    private static async Task<object> ReadDocumentStateAsync(IPage page)
    {
        try
        {
            return await page.EvaluateAsync<JsonElement>("""
                () => {
                    const navigation = performance.getEntriesByType('navigation')[0];
                    const round = value => value ? Math.round(value) : null;
                    return {
                        path: location.pathname,
                        readyState: document.readyState,
                        blazorDefined: typeof Blazor !== 'undefined',
                        loaderPresent: !!document.querySelector('#app .loader_main'),
                        errorUiVisible: !!document.getElementById('blazor-error-ui')
                            && getComputedStyle(document.getElementById('blazor-error-ui')).display !== 'none',
                        documentResponseEndMs: round(navigation?.responseEnd),
                        domContentLoadedMs: round(navigation?.domContentLoadedEventEnd),
                        loadEventMs: round(navigation?.loadEventEnd),
                        sinceNavigationStartMs: Math.round(performance.now()),
                    };
                }
                """).WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception exception)
        {
            return $"unavailable: {exception.GetType().Name}";
        }
    }

    // Out-of-browser probe, run only after a timeout: distinguishes a server that does not answer at all from
    // a browser that is waiting on one origin. All probes run in parallel with a 5-second limit each, without
    // cookies or credentials; only origin, path, status code and timing are recorded.
    private static async Task<object> ProbeAsync(IReadOnlyList<Uri> servers)
    {
        using var handler = new HttpClientHandler
        {
            UseCookies = false,
            UseDefaultCredentials = false,
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        return await Task.WhenAll(servers.Select(async server =>
        {
            var origin = server.GetLeftPart(UriPartial.Authority);
            var watch = Stopwatch.StartNew();
            try
            {
                using var response = await client.GetAsync(server, HttpCompletionOption.ResponseHeadersRead);
                return (object)new { origin, path = server.AbsolutePath, status = (int)response.StatusCode, elapsedMs = watch.ElapsedMilliseconds };
            }
            catch (Exception exception)
            {
                return new { origin, path = server.AbsolutePath, error = exception.GetType().Name, elapsedMs = watch.ElapsedMilliseconds };
            }
        }));
    }

    private static object ReadRunnerLoad()
    {
        string? Read(string path, int lines)
        {
            try
            {
                return File.Exists(path) ? string.Join(" | ", File.ReadLines(path).Take(lines)) : null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        return new
        {
            processorCount = Environment.ProcessorCount,
            loadAverage = Read("/proc/loadavg", 1),
            memory = Read("/proc/meminfo", 3),
        };
    }

    private void OnRequest(IRequest request)
    {
        var entry = new RequestEntry(OriginOf(request.Url), PathOf(request.Url), request.ResourceType, request.Method, clock.ElapsedMilliseconds);
        lock (gate)
        {
            requests[request] = entry;
            if (request.IsNavigationRequest && request.Frame.ParentFrame is null)
            {
                AddUnlocked($"document-request {entry.Origin}{entry.Path}");
            }
        }
    }

    private void OnResponse(IResponse response)
    {
        lock (gate)
        {
            if (requests.TryGetValue(response.Request, out var entry))
            {
                entry.Status = response.Status;
                if (response.Request.IsNavigationRequest && response.Request.Frame.ParentFrame is null)
                {
                    AddUnlocked($"document-response {response.Status} {entry.Origin}{entry.Path}");
                }
            }
        }
    }

    private void OnFinished(IRequest request, string? failure)
    {
        lock (gate)
        {
            if (requests.TryGetValue(request, out var entry))
            {
                entry.EndMs = clock.ElapsedMilliseconds;
                entry.Failure = failure;
            }
        }
    }

    private void Add(string value)
    {
        lock (gate)
        {
            AddUnlocked(value);
        }
    }

    private void AddUnlocked(string value)
    {
        if (events.Count < MaxEvents)
        {
            events.Add($"{clock.ElapsedMilliseconds,7} ms {value}");
        }
    }

    private static string OriginOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri.GetLeftPart(UriPartial.Authority) : "(other)";

    private static string PathOf(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? Redact(uri.AbsolutePath) : "(non-absolute)";

    // Drops URL queries and fragments (console messages can quote full URLs), then masks JWT- and
    // e-mail-shaped values and truncates.
    private static string Redact(string? value)
    {
        var redacted = UrlQueryPattern().Replace(value ?? string.Empty, "$1");
        redacted = EmailPattern().Replace(JwtPattern().Replace(redacted, "[redacted-jwt]"), "[redacted-email]");
        return redacted.Length <= MaxTextLength ? redacted : redacted[..MaxTextLength] + "…";
    }

    [GeneratedRegex(@"([a-z][a-z0-9+.-]*://[^\s?#""'<>]*)[?#][^\s""'<>]*", RegexOptions.IgnoreCase)]
    private static partial Regex UrlQueryPattern();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*")]
    private static partial Regex JwtPattern();

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailPattern();

    private sealed class RequestEntry(string origin, string path, string resourceType, string method, long startMs)
    {
        public string Origin { get; } = origin;

        public string Path { get; } = path;

        public string ResourceType { get; } = resourceType;

        public string Method { get; } = method;

        public long StartMs { get; } = startMs;

        public long? EndMs { get; set; }

        public int? Status { get; set; }

        public string? Failure { get; set; }
    }
}

/// <summary>
/// A navigation did not reach its load state. The message carries the redacted navigation diagnostics;
/// the original Playwright timeout is preserved as the inner exception.
/// </summary>
internal sealed class NavigationTimeoutException(string message, Exception innerException) : Exception(message, innerException);
