namespace BlazorShop.Web.Shared.Models.Analytics;

public class MetricsFilterModel
{
    public DateTime From { get; set; }

    public DateTime To { get; set; }

    public string Granularity { get; set; } = "day";
}
