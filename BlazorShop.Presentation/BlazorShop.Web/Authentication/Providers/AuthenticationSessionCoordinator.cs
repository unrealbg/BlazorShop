namespace BlazorShop.Web.Authentication.Providers;

using Microsoft.JSInterop;

public sealed class AuthenticationSessionCoordinator(IJSRuntime js) : IAuthenticationSessionCoordinator, IAsyncDisposable
{
    private IJSObjectReference? module;
    private DotNetObjectReference<AuthenticationSessionCoordinator>? reference;
    private readonly Dictionary<string, Func<Task>> actions = [];
    private string? executingEpoch;

    private async Task<IJSObjectReference> ModuleAsync() => module ??=
        await js.InvokeAsync<IJSObjectReference>("import", "./js/authSessionSync.js");

    public async Task<bool> RunAsync(string operation, Func<Task> action, Func<Task>? onIntent = null)
    {
        var id = Guid.NewGuid().ToString("N");
        try
        {
            var current = await ModuleAsync();
            var epoch = await current.InvokeAsync<string>("beginIntent", operation);
            if (onIntent is not null) await onIntent();
            reference ??= DotNetObjectReference.Create(this);
            actions.Add(id, action);
            return await current.InvokeAsync<string>("runExclusive", reference, id, operation, epoch) == "completed";
        }
        catch (JSException) { return false; }
        catch (TaskCanceledException) { return false; }
        finally { actions.Remove(id); }
    }

    [JSInvokable]
    public async Task ExecuteUnderLockAsync(string id, string epoch)
    {
        if (!actions.TryGetValue(id, out var action)) return;
        executingEpoch = epoch;
        try { await action(); }
        finally { executingEpoch = null; }
    }

    public async Task<bool> IsCurrentAsync() => executingEpoch is not null
        && await (await ModuleAsync()).InvokeAsync<bool>("isCurrent", executingEpoch);
    public async Task<bool> HasCurrentEpochAsync() => await (await ModuleAsync()).InvokeAsync<bool>("hasCurrentEpoch");
    public async Task<string> GetEpochAsync() => await (await ModuleAsync()).InvokeAsync<string>("currentEpoch");
    public async Task<bool> BeginRefreshAsync() => await (await ModuleAsync()).InvokeAsync<bool>("beginRefresh", executingEpoch);
    public async Task<bool> CommitTokenAsync(string token) => await (await ModuleAsync()).InvokeAsync<bool>("commitToken", executingEpoch, token);
    public async Task<bool> InvalidateCurrentAsync() => await (await ModuleAsync()).InvokeAsync<bool>("invalidateCurrent", executingEpoch);
    public async Task<bool> PublishSignedInAsync() => await (await ModuleAsync()).InvokeAsync<bool>("publishSignedIn", executingEpoch);

    public async ValueTask DisposeAsync()
    {
        reference?.Dispose();
        if (module is not null) await module.DisposeAsync();
    }
}
