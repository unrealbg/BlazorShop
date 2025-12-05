namespace BlazorShop.Web.Pages.Administration
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Globalization;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Analytics;
    using BlazorShop.Web.Shared.Services.Contracts;
    using Microsoft.AspNetCore.Components;
    using Microsoft.JSInterop;

    public partial class SalesPage : ComponentBase
    {
        private List<GetOrder> _orders = new();
        private List<GetOrder> _recentOrders = new();
        private decimal _totalRevenue;
        private decimal _todayRevenue;
        private int _countOrders;
        private int _pendingCount;
        private int _shippedCount;
        private int _inTransitCount;
        private int _ofdCount;
        private int _deliveredCount;

        private string _statusFilter = string.Empty;
        private string _refQuery = string.Empty;
        private DateTime? _fromDate;
        private DateTime? _toDate;
        private string _sortColumn = "CreatedOn";
        private bool _sortAsc = false;
        private List<GetOrder> _displayOrders = new();

        private List<(string Name, int Quantity, decimal Revenue)> _topProducts = new();

        private MetricsSeriesModel? _salesSeries;
        private MetricsSeriesModel? _trafficSeries;
        private bool _metricsLoading;
        private string _metricsGranularity = "day";
        private DateTime _metricsFrom = DateTime.UtcNow.Date.AddDays(-13);
        private DateTime _metricsTo = DateTime.UtcNow.Date;

        private bool _showEdit; 
        private GetOrder? _editOrder;
        private string _carrier = string.Empty;
        private string _trackingNumber = string.Empty;
        private string _trackingUrl = string.Empty;
        private string _shippingStatus = "PendingShipment";

        [Inject] private IJSRuntime JS { get; set; } = default!;
        [Inject] private IMetricsClient MetricsClient { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                var client = await this.HttpClientHelper.GetPrivateClientAsync();
                var api = new ApiCall { Client = client, Route = Constant.Cart.GetAllOrders, Type = Constant.ApiCallType.Get };
                var http = await this.ApiCallHelper.ApiCallTypeCall<object>(api);
                _orders = http is null || !http.IsSuccessStatusCode
                    ? new()
                    : (await this.ApiCallHelper.GetServiceResponse<IEnumerable<GetOrder>>(http)).ToList();
            }
            catch
            {
                _orders = new();
            }

            ComputeStats();
            ApplyFilters();
            BuildTopProducts();
            await LoadMetricsAsync();
        }

        private void ComputeStats()
        {
            _countOrders = _orders.Count;
            _totalRevenue = _orders.Sum(o => o.TotalAmount);
            var today = DateTime.UtcNow.Date;
            _todayRevenue = _orders.Where(o => o.CreatedOn.Date == today).Sum(o => o.TotalAmount);
            _pendingCount = _orders.Count(o => string.Equals(o.ShippingStatus, "PendingShipment", StringComparison.OrdinalIgnoreCase));
            _shippedCount = _orders.Count(o => string.Equals(o.ShippingStatus, "Shipped", StringComparison.OrdinalIgnoreCase));
            _inTransitCount = _orders.Count(o => string.Equals(o.ShippingStatus, "InTransit", StringComparison.OrdinalIgnoreCase));
            _ofdCount = _orders.Count(o => string.Equals(o.ShippingStatus, "OutForDelivery", StringComparison.OrdinalIgnoreCase));
            _deliveredCount = _orders.Count(o => string.Equals(o.ShippingStatus, "Delivered", StringComparison.OrdinalIgnoreCase));
            _recentOrders = _orders.OrderByDescending(o => o.CreatedOn).Take(50).ToList();
        }

        private void ApplyFilters()
        {
            IEnumerable<GetOrder> q = _recentOrders;
            if (!string.IsNullOrWhiteSpace(_statusFilter))
                q = q.Where(o => string.Equals(o.ShippingStatus, _statusFilter, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(_refQuery))
                q = q.Where(o => o.Reference.Contains(_refQuery, StringComparison.OrdinalIgnoreCase));
            if (_fromDate.HasValue)
                q = q.Where(o => o.CreatedOn.Date >= _fromDate.Value.Date);
            if (_toDate.HasValue)
                q = q.Where(o => o.CreatedOn.Date <= _toDate.Value.Date);

            q = (_sortColumn, _sortAsc) switch
            {
                ("Reference", true) => q.OrderBy(o => o.Reference),
                ("Reference", false) => q.OrderByDescending(o => o.Reference),
                ("TotalAmount", true) => q.OrderBy(o => o.TotalAmount),
                ("TotalAmount", false) => q.OrderByDescending(o => o.TotalAmount),
                ("ShippingStatus", true) => q.OrderBy(o => o.ShippingStatus),
                ("ShippingStatus", false) => q.OrderByDescending(o => o.ShippingStatus),
                ("CreatedOn", true) => q.OrderBy(o => o.CreatedOn),
                _ => q.OrderByDescending(o => o.CreatedOn)
            };

            _displayOrders = q.Take(10).ToList();
            StateHasChanged();
        }

        private void SortBy(string column)
        {
            if (_sortColumn == column)
            {
                _sortAsc = !_sortAsc;
            }
            else
            {
                _sortColumn = column;
                _sortAsc = false;
            }
            ApplyFilters();
        }

        private void BuildTopProducts()
        {
            _topProducts = _orders
                .SelectMany(o => o.Lines)
                .GroupBy(l => l.ProductName ?? "(Unknown)")
                .Select(g => (Name: g.Key, Quantity: g.Sum(x => x.Quantity), Revenue: g.Sum(x => x.LineTotal)))
                .OrderByDescending(x => x.Revenue)
                .Take(5)
                .ToList();
        }

        private void OpenEdit(GetOrder order)
        {
            _editOrder = order;
            _carrier = order.ShippingCarrier ?? string.Empty;
            _trackingNumber = order.TrackingNumber ?? string.Empty;
            _trackingUrl = order.TrackingUrl ?? string.Empty;
            _shippingStatus = order.ShippingStatus ?? "PendingShipment";
            _showEdit = true;
        }

        private async Task SaveEditAsync()
        {
            if (_editOrder is null) return;
            var client = await this.HttpClientHelper.GetPrivateClientAsync();
            var tApi = new ApiCall { Client = client, Route = $"{Constant.Cart.GetAllOrders}/{_editOrder.Id}/tracking", Type = Constant.ApiCallType.Update, Model = new { Carrier = _carrier, TrackingNumber = _trackingNumber, TrackingUrl = _trackingUrl } };
            var sApi = new ApiCall { Client = client, Route = $"{Constant.Cart.GetAllOrders}/{_editOrder.Id}/shipping-status", Type = Constant.ApiCallType.Update, Model = new { ShippingStatus = _shippingStatus } };

            var tRes = await this.ApiCallHelper.ApiCallTypeCall<object>(tApi);
            var sRes = await this.ApiCallHelper.ApiCallTypeCall<object>(sApi);

            _showEdit = false;

            if (tRes.IsSuccessStatusCode && sRes.IsSuccessStatusCode)
            {
                this.ToastService.ShowSuccessToast("Saved");
                await OnInitializedAsync();
            }
            else
            {
                this.ToastService.ShowErrorToast("Failed to save");
            }
        }

        private async Task LoadMetricsAsync()
        {
            _metricsLoading = true;
            StateHasChanged();

            try
            {
                var filter = new MetricsFilterModel
                {
                    From = _metricsFrom,
                    To = _metricsTo,
                    Granularity = _metricsGranularity
                };

                var salesTask = MetricsClient.GetSalesAsync(filter);
                var trafficTask = MetricsClient.GetTrafficAsync(filter);
                await Task.WhenAll(salesTask, trafficTask);

                _salesSeries = salesTask.Result;
                _trafficSeries = trafficTask.Result;

                if (_salesSeries is null || _trafficSeries is null)
                {
                    _salesSeries = null;
                    _trafficSeries = null;
                    ToastService.ShowErrorToast("Failed to load analytics data.");
                    return;
                }

                await RenderChartsAsync();
            }
            catch
            {
                ToastService.ShowErrorToast("Failed to load analytics data.");
            }
            finally
            {
                _metricsLoading = false;
                StateHasChanged();
            }
        }

        private async Task RenderChartsAsync()
        {
            if (_salesSeries is not null)
            {
                var labels = BuildLabels(_salesSeries);
                var values = _salesSeries.Points.Select(p => p.Value).ToArray();
                await JS.InvokeVoidAsync(
                    "blz.renderLineChart",
                    "salesChart",
                    labels,
                    values,
                    new
                    {
                        label = "Revenue (€)",
                        color = "#0ea5e9",
                        backgroundColor = "rgba(14,165,233,0.15)"
                    });
            }

            if (_trafficSeries is not null)
            {
                var labels = BuildLabels(_trafficSeries);
                var values = _trafficSeries.Points.Select(p => p.Value).ToArray();
                await JS.InvokeVoidAsync(
                    "blz.renderLineChart",
                    "trafficChart",
                    labels,
                    values,
                    new
                    {
                        label = "Traffic (sign-ups)",
                        color = "#a855f7",
                        backgroundColor = "rgba(168,85,247,0.15)"
                    });
            }
        }

        private static string[] BuildLabels(MetricsSeriesModel series)
        {
            return series.Points
                .Select(point => FormatLabel(point.PeriodStart, series.Granularity))
                .ToArray();
        }

        private static string FormatLabel(DateTime timestamp, string? granularity)
        {
            var token = granularity?.ToLowerInvariant();
            return token switch
            {
                "week" => $"W{ISOWeek.GetWeekOfYear(timestamp)}",
                "month" => timestamp.ToString("MMM yyyy", CultureInfo.InvariantCulture),
                _ => timestamp.ToString("dd MMM", CultureInfo.InvariantCulture)
            };
        }

        private async Task ApplyQuickRangeAsync(int days)
        {
            if (days <= 0)
            {
                return;
            }

            _metricsTo = DateTime.UtcNow.Date;
            _metricsFrom = _metricsTo.AddDays(-(days - 1));
            await LoadMetricsAsync();
        }

        private async Task OnMetricsDateChangedAsync(ChangeEventArgs args, bool isStart)
        {
            if (!DateTime.TryParse(args.Value?.ToString(), out var parsed))
            {
                return;
            }

            var normalized = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);

            if (isStart)
            {
                _metricsFrom = normalized;
            }
            else
            {
                _metricsTo = normalized;
            }

            if (_metricsFrom > _metricsTo)
            {
                ToastService.ShowErrorToast("Invalid date range.");
                return;
            }

            await LoadMetricsAsync();
        }

        private async Task OnGranularityChangedAsync(ChangeEventArgs args)
        {
            var value = args.Value?.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            _metricsGranularity = value;
            await LoadMetricsAsync();
        }

        private string GetMetricsRangeLabel()
        {
            return $"{_metricsFrom.ToLocalTime():dd MMM} - {_metricsTo.ToLocalTime():dd MMM}";
        }

        private static string FormatTrend(decimal value)
        {
            if (value > 0)
            {
                return $"+{value:F1}%";
            }

            if (value < 0)
            {
                return $"{value:F1}%";
            }

            return "0%";
        }

        private static string GetTrendClass(decimal value)
        {
            if (value > 0)
            {
                return "text-emerald-600";
            }

            if (value < 0)
            {
                return "text-rose-600";
            }

            return "text-neutral-600";
        }

        private bool IsQuickRangeSelected(int days)
        {
            var span = (_metricsTo.Date - _metricsFrom.Date).TotalDays + 1;
            return Math.Abs(span - days) < 0.1;
        }

        private async Task ExportCsvAsync()
        {
            var header = "Created,Reference,Status,Total,Tracking";
            var lines = _orders.Select(o => string.Join(',',
                o.CreatedOn.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                Escape(o.Reference),
                Escape(o.ShippingStatus),
                o.TotalAmount.ToString("F2"),
                Escape(!string.IsNullOrWhiteSpace(o.TrackingUrl) ? o.TrackingUrl! : o.TrackingNumber ?? "")));
            var csv = string.Join("\n", new[] { header }.Concat(lines));
            await JS.InvokeVoidAsync("blz.downloadFile", $"orders_{DateTime.UtcNow:yyyyMMddHHmm}.csv", csv, "text/csv;charset=utf-8");
        }

        private static string Escape(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var needs = s.Contains('"') || s.Contains(',') || s.Contains('\n');
            s = s.Replace("\"", "\"\"");
            return needs ? $"\"{s}\"" : s;
        }
    }
}