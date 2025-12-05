namespace BlazorShop.Web.Shared.Models.Analytics;

public class MetricsSeriesModel
{
    public string Series { get; set; } = string.Empty;

    public DateTime From { get; set; }

    public DateTime To { get; set; }

    public string Granularity { get; set; } = "Day";

    public decimal Total { get; set; }

    public decimal PreviousTotal { get; set; }

    public decimal TrendPercentage { get; set; }

    public List<MetricPointModel> Points { get; set; } = new();
}
