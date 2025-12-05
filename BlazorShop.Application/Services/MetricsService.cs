namespace BlazorShop.Application.Services
{
    using BlazorShop.Application.DTOs.Analytics;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Domain.Contracts.Newsletters;
    using BlazorShop.Domain.Contracts.Payment;

    using System.Linq;

    public class MetricsService(IOrderRepository orderRepository, INewsletterSubscriberRepository newsletterRepository)
        : IMetricsService
    {
        private readonly IOrderRepository _orderRepository = orderRepository;
        private readonly INewsletterSubscriberRepository _newsletterRepository = newsletterRepository;

        public async Task<MetricsSeriesDto> GetSalesAsync(DateTime fromUtc, DateTime toUtc, MetricsGranularity granularity)
        {
            var range = NormalizeRange(fromUtc, toUtc);
            var orders = await _orderRepository.GetByDateRangeAsync(range.FromInclusiveUtc, range.ToInclusiveUtc);
            var data = orders.Select(o => (Timestamp: EnsureUtc(o.CreatedOn), Value: o.TotalAmount));
            return await BuildSeriesAsync("Sales", granularity, range, data, isSalesSeries: true);
        }

        public async Task<MetricsSeriesDto> GetTrafficAsync(DateTime fromUtc, DateTime toUtc, MetricsGranularity granularity)
        {
            var range = NormalizeRange(fromUtc, toUtc);
            var subscribers = await _newsletterRepository.GetByDateRangeAsync(range.FromInclusiveUtc, range.ToInclusiveUtc);
            var data = subscribers.Select(s => (Timestamp: EnsureUtc(s.CreatedOn), Value: 1m));
            return await BuildSeriesAsync("Traffic", granularity, range, data, isSalesSeries: false);
        }

        private async Task<MetricsSeriesDto> BuildSeriesAsync(
            string seriesName,
            MetricsGranularity requestedGranularity,
            NormalizedRange range,
            IEnumerable<(DateTime Timestamp, decimal Value)> currentSource,
            bool isSalesSeries)
        {
            var granularity = ValidateGranularity(requestedGranularity);
            var filteredSource = currentSource
                .Where(x => x.Timestamp >= range.FromInclusiveUtc && x.Timestamp <= range.ToInclusiveUtc)
                .ToList();

            var points = BuildPoints(filteredSource, granularity, range.FromInclusiveUtc, range.ToInclusiveUtc);
            var total = points.Sum(p => p.Value);

            var previousRange = range.GetPreviousRange();
            IEnumerable<(DateTime Timestamp, decimal Value)> previousSource;

            if (isSalesSeries)
            {
                var prevOrders = await _orderRepository.GetByDateRangeAsync(previousRange.FromInclusiveUtc, previousRange.ToInclusiveUtc);
                previousSource = prevOrders.Select(o => (Timestamp: EnsureUtc(o.CreatedOn), Value: o.TotalAmount));
            }
            else
            {
                var prevSubscribers = await _newsletterRepository.GetByDateRangeAsync(previousRange.FromInclusiveUtc, previousRange.ToInclusiveUtc);
                previousSource = prevSubscribers.Select(s => (Timestamp: EnsureUtc(s.CreatedOn), Value: 1m));
            }

            var previousPoints = BuildPoints(previousSource, granularity, previousRange.FromInclusiveUtc, previousRange.ToInclusiveUtc);
            var previousTotal = previousPoints.Sum(p => p.Value);
            var trend = CalculateTrend(total, previousTotal);

            return new MetricsSeriesDto
            {
                Series = seriesName,
                From = range.FromInclusiveUtc,
                To = range.ToInclusiveUtc,
                Granularity = granularity.ToString(),
                Total = decimal.Round(total, 2),
                PreviousTotal = decimal.Round(previousTotal, 2),
                TrendPercentage = trend,
                Points = points
            };
        }

        private static List<MetricPointDto> BuildPoints(
            IEnumerable<(DateTime Timestamp, decimal Value)> source,
            MetricsGranularity granularity,
            DateTime fromUtc,
            DateTime toUtc)
        {
            var buckets = source
                .GroupBy(x => AlignToBucket(x.Timestamp, granularity))
                .ToDictionary(g => g.Key, g => g.Sum(i => i.Value));

            var cursor = AlignToBucket(fromUtc, granularity);
            var end = AlignToBucket(toUtc, granularity);

            var points = new List<MetricPointDto>();
            while (cursor <= end)
            {
                points.Add(new MetricPointDto
                {
                    PeriodStart = cursor,
                    Value = buckets.TryGetValue(cursor, out var value) ? decimal.Round(value, 2) : 0m
                });

                cursor = IncrementBucket(cursor, granularity);
            }

            return points;
        }

        private static MetricsGranularity ValidateGranularity(MetricsGranularity granularity) =>
            Enum.IsDefined(typeof(MetricsGranularity), granularity) ? granularity : MetricsGranularity.Day;

        private static decimal CalculateTrend(decimal currentTotal, decimal previousTotal)
        {
            if (previousTotal == 0)
            {
                return currentTotal == 0 ? 0 : 100;
            }

            return decimal.Round(((currentTotal - previousTotal) / previousTotal) * 100, 2);
        }

        private static DateTime AlignToBucket(DateTime timestamp, MetricsGranularity granularity)
        {
            timestamp = EnsureUtc(timestamp);
            return granularity switch
            {
                MetricsGranularity.Week => StartOfWeek(timestamp),
                MetricsGranularity.Month => new DateTime(timestamp.Year, timestamp.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                _ => new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 0, 0, 0, DateTimeKind.Utc)
            };
        }

        private static DateTime IncrementBucket(DateTime timestamp, MetricsGranularity granularity) => granularity switch
        {
            MetricsGranularity.Week => timestamp.AddDays(7),
            MetricsGranularity.Month => timestamp.AddMonths(1),
            _ => timestamp.AddDays(1)
        };

        private static DateTime StartOfWeek(DateTime timestamp)
        {
            var diff = (7 + (timestamp.DayOfWeek - DayOfWeek.Monday)) % 7;
            var monday = timestamp.AddDays(-diff).Date;
            return DateTime.SpecifyKind(monday, DateTimeKind.Utc);
        }

        private static DateTime EnsureUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private static NormalizedRange NormalizeRange(DateTime fromUtc, DateTime toUtc)
        {
            var fromDate = DateOnly.FromDateTime(EnsureUtc(fromUtc));
            var toDate = DateOnly.FromDateTime(EnsureUtc(toUtc));
            if (toDate < fromDate)
            {
                throw new ArgumentException("The end date must be greater than or equal to the start date.");
            }

            var from = fromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var to = toDate.ToDateTime(new TimeOnly(23, 59, 59, 999), DateTimeKind.Utc);
            return new NormalizedRange(fromDate, toDate, from, to);
        }

        private sealed record NormalizedRange(DateOnly FromDate, DateOnly ToDate, DateTime FromInclusiveUtc, DateTime ToInclusiveUtc)
        {
            public NormalizedRange GetPreviousRange()
            {
                var days = Math.Max(1, ToDate.DayNumber - FromDate.DayNumber + 1);
                var previousEndDate = FromDate.AddDays(-1);
                var previousStartDate = previousEndDate.AddDays(-(days - 1));

                var from = previousStartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var to = previousEndDate.ToDateTime(new TimeOnly(23, 59, 59, 999), DateTimeKind.Utc);
                return new NormalizedRange(previousStartDate, previousEndDate, from, to);
            }
        }
    }
}