namespace BlazorShop.Web.Authentication.Providers
{
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Helper.Contracts;

    using Microsoft.JSInterop;

    public sealed class AuthenticationSessionSyncService : IAuthenticationSessionSyncService
    {
        private const string ModulePath = "./js/authSessionSync.js";

        private readonly IAuthenticationSessionRefresher _sessionRefresher;
        private readonly ITokenService _tokenService;
        private readonly IAuthenticatedClientStateCleaner _clientStateCleaner;
        private readonly IAuthenticationStateNotifier _authenticationStateNotifier;
        private readonly IJSRuntime _jsRuntime;
        private readonly IAuthenticationSessionCoordinator _coordinator;

        private IJSObjectReference? _module;
        private DotNetObjectReference<AuthenticationSessionSyncService>? _selfReference;

        public AuthenticationSessionSyncService(
            IAuthenticationSessionRefresher sessionRefresher,
            ITokenService tokenService,
            IAuthenticatedClientStateCleaner clientStateCleaner,
            IAuthenticationStateNotifier authenticationStateNotifier,
            IJSRuntime jsRuntime,
            IAuthenticationSessionCoordinator coordinator)
        {
            _sessionRefresher = sessionRefresher;
            _tokenService = tokenService;
            _clientStateCleaner = clientStateCleaner;
            _authenticationStateNotifier = authenticationStateNotifier;
            _jsRuntime = jsRuntime;
            _coordinator = coordinator;
        }

        public async Task InitializeAsync()
        {
            if (_module is not null)
            {
                return;
            }

            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
            _selfReference = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("subscribe", _selfReference);
        }

        [JSInvokable]
        public async Task HandleAuthSessionEventAsync(string eventType)
        {
            switch (eventType)
            {
                case "signed-in":
                case "session-refreshed":
                    var onlyIfNeeded = eventType == "signed-in";
                    var epoch = await _coordinator.GetEpochAsync();
                    var refreshed = await _sessionRefresher.TryRefreshAsync(onlyIfNeeded: onlyIfNeeded);
                    // A newer credential login can supersede a flight already in this
                    // tab. Re-evaluate that announced session after the old flight ends.
                    // Failed/uncertain refresh changes the epoch to signed-out, so this
                    // cannot retry a credential with an unknown outcome.
                    if (refreshed is null && await _coordinator.IsSignedInAsync(epoch))
                        await _sessionRefresher.TryRefreshAsync(onlyIfNeeded: onlyIfNeeded);
                    break;
                case "session-changing":
                case "signed-out":
                    var hadToken = !string.IsNullOrWhiteSpace(await _tokenService.GetJwtTokenAsync(Constant.TokenStorage.Key));
                    await _tokenService.RemoveJwtTokenAsync(Constant.TokenStorage.Key);
                    await _clientStateCleaner.ClearAsync();
                    if (hadToken) _authenticationStateNotifier.NotifyAuthenticationState();
                    break;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_module is not null)
            {
                try
                {
                    await _module.InvokeVoidAsync("unsubscribe");
                    await _module.DisposeAsync();
                }
                catch
                {
                    // Disposal should never block app shutdown.
                }
            }

            _selfReference?.Dispose();
        }
    }
}