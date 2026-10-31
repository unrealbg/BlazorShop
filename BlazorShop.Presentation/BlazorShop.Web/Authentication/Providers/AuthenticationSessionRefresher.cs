namespace BlazorShop.Web.Authentication.Providers;

using System.IdentityModel.Tokens.Jwt;
using BlazorShop.Web.Shared;
using BlazorShop.Web.Shared.Helper.Contracts;
using BlazorShop.Web.Shared.Models;
using BlazorShop.Web.Shared.Services.Contracts;

public sealed class AuthenticationSessionRefresher(
    ITokenService tokenService,
    IAuthenticationService authenticationService,
    IAuthenticationStateNotifier notifier,
    IAuthenticatedClientStateCleaner cleaner,
    IAuthenticationSessionCoordinator coordinator) : IAuthenticationSessionRefresher
{
    private readonly object gate = new();
    private Task<LoginResponse?>? inFlight;
    private string? lastRejectedToken;
    private string? lastIssuedToken;
    private string? lastIssuedEpoch;

    public Task<LoginResponse?> TryRefreshAsync(string? rejectedToken = null, bool onlyIfNeeded = false)
    {
        lock (gate)
        {
            if (inFlight is not null) return inFlight;
            var completion = new TaskCompletionSource<LoginResponse?>(TaskCreationOptions.RunContinuationsAsynchronously);
            inFlight = completion.Task;
            _ = CompleteAsync(completion, rejectedToken, onlyIfNeeded);
            return completion.Task;
        }
    }

    private async Task CompleteAsync(TaskCompletionSource<LoginResponse?> completion, string? rejectedToken, bool onlyIfNeeded)
    {
        LoginResponse? result = null;
        try
        {
            var startingEpoch = await coordinator.GetEpochAsync();
            var executed = await coordinator.RunAsync("refresh", async () =>
            {
                // Re-evaluate AFTER acquiring the cross-tab lock. A late 401 burst must
                // reuse this tab's newer JWT instead of rotating the cookie again.
                var epoch = await coordinator.GetEpochAsync();
                var token = await tokenService.GetJwtTokenAsync(Constant.TokenStorage.Key);
                if (await coordinator.HasCurrentEpochAsync() && IsCurrentToken(token)
                    && (onlyIfNeeded || (rejectedToken is not null && (token != rejectedToken || (epoch == lastIssuedEpoch && token == lastIssuedToken && rejectedToken == lastRejectedToken)))))
                {
                    result = new LoginResponse(true, "Session available.", token);
                    return;
                }
                if (!await coordinator.BeginRefreshAsync()) return;
                QueryResult<LoginResponse> response;
                try { response = await authenticationService.ReviveToken(); }
                catch
                {
                    if (await coordinator.InvalidateCurrentAsync()) await ClearAsync();
                    return;
                }
                if (!await coordinator.IsCurrentAsync()) return;
                if (response.Success && !string.IsNullOrWhiteSpace(response.Data?.Token))
                {
                    if (!await coordinator.CommitTokenAsync(response.Data.Token)) return;
                    lastRejectedToken = rejectedToken;
                    lastIssuedToken = response.Data.Token;
                    lastIssuedEpoch = epoch;
                    notifier.NotifyAuthenticationState();
                    result = response.Data;
                    return;
                }
                // Includes uncertain network/commit outcomes. No automatic recovery or
                // retry of a possibly consumed cookie; require explicit sign-in.
                if (await coordinator.InvalidateCurrentAsync()) await ClearAsync();
            });
            if (!executed && startingEpoch == await coordinator.GetEpochAsync()) await ClearAsync();
        }
        catch
        {
            await ClearAsync();
            // No raw exception/credential is surfaced by the session recovery path.
        }
        finally
        {
            lock (gate) inFlight = null;
            completion.TrySetResult(result);
        }
    }

    private async Task ClearAsync()
    {
        await tokenService.RemoveJwtTokenAsync(Constant.TokenStorage.Key);
        await cleaner.ClearAsync();
        notifier.NotifyAuthenticationState();
    }

    private static bool IsCurrentToken(string? value)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            return !string.IsNullOrWhiteSpace(value) && handler.CanReadToken(value)
                && handler.ReadJwtToken(value).ValidTo > DateTime.UtcNow;
        }
        catch (ArgumentException) { return false; }
    }
}
