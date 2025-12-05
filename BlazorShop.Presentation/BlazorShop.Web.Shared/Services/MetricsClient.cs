namespace BlazorShop.Web.Shared.Services
{
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Analytics;
    using BlazorShop.Web.Shared.Services.Contracts;

    public class MetricsClient(IHttpClientHelper httpClientHelper, IApiCallHelper apiCallHelper) : IMetricsClient
    {
        private readonly IHttpClientHelper _httpClientHelper = httpClientHelper;
        private readonly IApiCallHelper _apiCallHelper = apiCallHelper;

        public Task<MetricsSeriesModel?> GetSalesAsync(MetricsFilterModel filter)
            => GetAsync(Constant.Metrics.Sales, filter);

        public Task<MetricsSeriesModel?> GetTrafficAsync(MetricsFilterModel filter)
            => GetAsync(Constant.Metrics.Traffic, filter);

        private async Task<MetricsSeriesModel?> GetAsync(string route, MetricsFilterModel filter)
        {
            ArgumentNullException.ThrowIfNull(filter);

            try
            {
                var client = await _httpClientHelper.GetPrivateClientAsync();
                var apiCall = new ApiCall
                {
                    Route = $"{route}{BuildQuery(filter)}",
                    Type = Constant.ApiCallType.Get,
                    Client = client
                };

                var response = await _apiCallHelper.ApiCallTypeCall<Unit>(apiCall);
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await _apiCallHelper.GetServiceResponse<MetricsSeriesModel>(response);
            }
            catch
            {
                return null;
            }
        }

        private static string BuildQuery(MetricsFilterModel filter)
        {
            var from = EnsureUtc(filter.From).ToString("O");
            var to = EnsureUtc(filter.To).ToString("O");
            var granularity = string.IsNullOrWhiteSpace(filter.Granularity) ? "day" : filter.Granularity.Trim().ToLowerInvariant();
            return $"?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}&granularity={Uri.EscapeDataString(granularity)}";
        }

        private static DateTime EnsureUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}