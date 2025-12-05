namespace BlazorShop.Application.DTOs.Analytics
{
    public class MetricsSeriesDto
    {
        public string Series { get; set; } = string.Empty;

        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public string Granularity { get; set; } = MetricsGranularity.Day.ToString();

        public decimal Total { get; set; }

        public decimal PreviousTotal { get; set; }

        public decimal TrendPercentage { get; set; }

        public List<MetricPointDto> Points { get; set; } = new();
    }
}