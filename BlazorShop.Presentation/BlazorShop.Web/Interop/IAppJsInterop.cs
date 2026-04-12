namespace BlazorShop.Web.Interop
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.JSInterop;

    public interface IAppJsInterop : IAsyncDisposable
    {
        ValueTask DownloadFileAsync(
            string fileName,
            string content,
            string contentType,
            CancellationToken cancellationToken = default);

        ValueTask RenderLineChartAsync(
            string canvasId,
            IReadOnlyList<string> labels,
            IReadOnlyList<decimal> values,
            ChartSeriesOptions dataset,
            CancellationToken cancellationToken = default);

        ValueTask DisposeChartAsync(string canvasId, CancellationToken cancellationToken = default);
    }

    public sealed record ChartSeriesOptions(
        string Label,
        string Color,
        string BackgroundColor,
        double? Tension = null,
        double? BorderWidth = null,
        double? PointRadius = null,
        bool? Fill = null);
}
