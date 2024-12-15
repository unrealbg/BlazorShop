namespace BlazorShop.Web.Shared.Interop
{
    using System;
    using System.Threading.Tasks;

    using BlazorShop.Web.Shared.Interop.Contracts;

    using Microsoft.JSInterop;

    public class JsModuleHandler : IJsModuleHandler
    {
        private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

        public JsModuleHandler(IJSRuntime jsRuntime, string modulePath)
        {
            if (jsRuntime == null)
            {
                throw new ArgumentNullException(nameof(jsRuntime));
            }

            if (string.IsNullOrWhiteSpace(modulePath))
            {
                throw new ArgumentException("Module path cannot be null or empty.", nameof(modulePath));
            }

            _moduleTask = new Lazy<Task<IJSObjectReference>>(() =>
                jsRuntime.InvokeAsync<IJSObjectReference>("import", modulePath).AsTask());
        }

        public async Task InvokeVoidAsync(string method, params object[] args)
        {
            var module = await _moduleTask.Value;
            await module.InvokeVoidAsync(method, args);
        }

        public async Task<T> InvokeAsync<T>(string method, params object[] args)
        {
            var module = await _moduleTask.Value;
            return await module.InvokeAsync<T>(method, args);
        }

        public async ValueTask DisposeAsync()
        {
            if (_moduleTask.IsValueCreated)
            {
                var module = await _moduleTask.Value;
                await module.DisposeAsync();
            }
        }
    }
}
