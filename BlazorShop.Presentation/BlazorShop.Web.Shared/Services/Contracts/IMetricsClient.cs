namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models.Analytics;

    public interface IMetricsClient
    {
        Task<MetricsSeriesModel?> GetSalesAsync(MetricsFilterModel filter);

        Task<MetricsSeriesModel?> GetTrafficAsync(MetricsFilterModel filter);
    }
}