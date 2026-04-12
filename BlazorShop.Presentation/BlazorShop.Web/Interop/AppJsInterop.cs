namespace BlazorShop.Web.Interop
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.JSInterop;

    public sealed class AppJsInterop : IAppJsInterop
    {
        private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

        public AppJsInterop(IJSRuntime jsRuntime)
        {
            _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/interop.js").AsTask());
        }

        public async ValueTask DownloadFileAsync(
            string fileName,
            string content,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("downloadFile", cancellationToken, fileName, content, contentType);
        }

        public async ValueTask RenderLineChartAsync(
            string canvasId,
            IReadOnlyList<string> labels,
            IReadOnlyList<decimal> values,
            ChartSeriesOptions dataset,
            CancellationToken cancellationToken = default)
        {
            var module = await GetModuleAsync();
            var typedValues = values?.Select(Convert.ToDouble).ToArray() ?? Array.Empty<double>();
            var payload = new
            {
                label = dataset.Label,
                color = dataset.Color,
                backgroundColor = dataset.BackgroundColor,
                tension = dataset.Tension,
                borderWidth = dataset.BorderWidth,
                pointRadius = dataset.PointRadius,
                fill = dataset.Fill,
            };

            await module.InvokeVoidAsync("renderLineChart", cancellationToken, canvasId, labels ?? Array.Empty<string>(), typedValues, payload);
        }

        public async ValueTask DisposeChartAsync(string canvasId, CancellationToken cancellationToken = default)
        {
            if (!_moduleTask.IsValueCreated)
            {
                return;
            }

            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("disposeChart", cancellationToken, canvasId);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_moduleTask.IsValueCreated)
            {
                return;
            }

            var module = await GetModuleAsync();
            await module.InvokeVoidAsync("disposeAllCharts");
            await module.DisposeAsync();
        }

        private ValueTask<IJSObjectReference> GetModuleAsync()
        {
            return new ValueTask<IJSObjectReference>(_moduleTask.Value);
        }
    }
}
