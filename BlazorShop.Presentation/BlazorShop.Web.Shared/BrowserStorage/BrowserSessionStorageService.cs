namespace BlazorShop.Web.Shared.BrowserStorage
{
    using System;
    using System.Threading.Tasks;

    using BlazorShop.Web.Shared.BrowserStorage.Contracts;
    using BlazorShop.Web.Shared.Interop;

    using Microsoft.JSInterop;

    public sealed class BrowserSessionStorageService : IBrowserSessionStorageService, IAsyncDisposable
    {
        private readonly JsModuleHandler _jsModuleHandler;

        public BrowserSessionStorageService(IJSRuntime jsRuntime)
        {
            _jsModuleHandler = new JsModuleHandler(jsRuntime, "./js/sessionStorage.js");
        }

        public async Task SetAsync(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Storage key cannot be null, empty, or whitespace.", nameof(key));
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Storage value cannot be null, empty, or whitespace.", nameof(value));
            }

            await _jsModuleHandler.InvokeVoidAsync("setItem", key, value);
        }

        public async Task<string?> GetAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Storage key cannot be null, empty, or whitespace.", nameof(key));
            }

            return await _jsModuleHandler.InvokeAsync<string?>("getItem", key);
        }

        public async Task RemoveAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Storage key cannot be null, empty, or whitespace.", nameof(key));
            }

            await _jsModuleHandler.InvokeVoidAsync("removeItem", key);
        }

        public async ValueTask DisposeAsync()
        {
            await _jsModuleHandler.DisposeAsync();
        }
    }
}