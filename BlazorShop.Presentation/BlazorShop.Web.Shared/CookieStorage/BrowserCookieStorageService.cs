namespace BlazorShop.Web.Shared.CookieStorage
{
    using System;
    using System.Threading.Tasks;

    using BlazorShop.Web.Shared.CookieStorage.Contracts;
    using BlazorShop.Web.Shared.Interop;

    using Microsoft.JSInterop;

    public class BrowserCookieStorageService : IBrowserCookieStorageService, IAsyncDisposable
    {
        private readonly JsModuleHandler _jsModuleHandler;

        public BrowserCookieStorageService(IJSRuntime jsRuntime)
        {
            _jsModuleHandler = new JsModuleHandler(jsRuntime, "./js/cookieStorage.js");
        }

        public async Task SetAsync(string name, string value, int days, string path = "/")
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Cookie name cannot be null, empty, or whitespace.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("Cookie value cannot be null, empty, or whitespace.", nameof(value));
            }

            await _jsModuleHandler.InvokeVoidAsync("setCookie", name, value, days, path);
        }

        public async Task<string?> GetAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Cookie name cannot be null, empty, or whitespace.", nameof(name));
            }

            return await _jsModuleHandler.InvokeAsync<string?>("getCookie", name);
        }

        public async Task RemoveAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Cookie name cannot be null, empty, or whitespace.", nameof(name));
            }

            await _jsModuleHandler.InvokeVoidAsync("removeCookie", name);
        }

        public async ValueTask DisposeAsync()
        {
            await _jsModuleHandler.DisposeAsync();
        }
    }
}