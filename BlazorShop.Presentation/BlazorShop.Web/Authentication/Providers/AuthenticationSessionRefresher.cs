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
    IAuthenticationSessionEventPublisher publisher,
    IAuthenticationSessionCoordinator coordinator) : IAuthenticationSessionRefresher
{
    private readonly object gate = new();
    private Task<LoginResponse?>? inFlight;

    public Task<LoginResponse?> TryRefreshAsync(bool clearTokenOnFailure = true, string? rejectedToken = null, bool onlyIfNeeded = false)
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
            var executed = await coordinator.RunAsync("refresh", async () =>
            {
                // Re-evaluate AFTER acquiring the cross-tab lock. A late 401 burst must
                // reuse this tab's newer JWT instead of rotating the cookie again.
                var token = await tokenService.GetJwtTokenAsync(Constant.TokenStorage.Key);
                if (await coordinator.HasCurrentEpochAsync() && IsCurrentToken(token)
                    && (onlyIfNeeded || (rejectedToken is not null && token != rejectedToken)))
                {
                    result = new LoginResponse(true, "Session available.", token);
                    return;
                }
                var response = await authenticationService.ReviveToken();
                if (!await coordinator.IsCurrentAsync()) return;
                if (response.Success && !string.IsNullOrWhiteSpace(response.Data?.Token))
                {
                    await tokenService.StoreJwtTokenAsync(Constant.TokenStorage.Key, response.Data.Token);
                    if (!await coordinator.IsCurrentAsync())
                    {
                        await tokenService.RemoveJwtTokenAsync(Constant.TokenStorage.Key);
                        return;
                    }
                    await coordinator.MarkCurrentEpochAsync();
                    notifier.NotifyAuthenticationState();
                    result = response.Data;
                    return;
                }
                // Includes uncertain network/commit outcomes. No automatic recovery or
                // retry of a possibly consumed cookie; require explicit sign-in.
                await ClearAsync();
                await publisher.PublishSignedOutAsync();
            });
            if (!executed) await ClearAsync();
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
