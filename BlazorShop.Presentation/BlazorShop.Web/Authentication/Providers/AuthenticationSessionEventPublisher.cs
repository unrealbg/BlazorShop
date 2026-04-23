namespace BlazorShop.Web.Authentication.Providers
{
    using Microsoft.JSInterop;

    public sealed class AuthenticationSessionEventPublisher : IAuthenticationSessionEventPublisher, IAsyncDisposable
    {
        private const string ModulePath = "./js/authSessionSync.js";

        private readonly IJSRuntime _jsRuntime;
        private IJSObjectReference? _module;

        public AuthenticationSessionEventPublisher(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public Task PublishSignedInAsync()
        {
            return PublishAsync("signed-in");
        }

        public Task PublishSignedOutAsync()
        {
            return PublishAsync("signed-out");
        }

        public Task PublishSessionRefreshedAsync()
        {
            return PublishAsync("session-refreshed");
        }

        public async ValueTask DisposeAsync()
        {
            if (_module is not null)
            {
                await _module.DisposeAsync();
            }
        }

        private async Task PublishAsync(string eventType)
        {
            try
            {
                _module ??= await _jsRuntime.InvokeAsync<IJSObjectReference>("import", ModulePath);
                await _module.InvokeVoidAsync("publish", eventType);
            }
            catch
            {
                // Cross-tab sync should not block the local auth flow.
            }
        }
    }
}