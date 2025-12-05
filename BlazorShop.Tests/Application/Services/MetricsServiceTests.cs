namespace BlazorShop.Tests.Application.Services
{
    using System.Linq;

    using BlazorShop.Application.DTOs.Analytics;
    using BlazorShop.Application.Services;
    using BlazorShop.Domain.Contracts.Newsletters;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Payment;

    using Moq;

    using Xunit;

    public class MetricsServiceTests
    {
        private readonly Mock<IOrderRepository> _orderRepository = new();
        private readonly Mock<INewsletterSubscriberRepository> _newsletterRepository = new();
        private readonly MetricsService _sut;

        public MetricsServiceTests()
        {
            _sut = new MetricsService(_orderRepository.Object, _newsletterRepository.Object);
        }

        [Fact]
        public async Task GetSalesAsync_ReturnsDailyBucketsAndTrend()
        {
            // Arrange
            var from = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = from.AddDays(6);
            var currentOrders = Enumerable.Range(0, 7)
                .Select(offset => new Order
                {
                    CreatedOn = from.AddDays(offset).AddHours(10),
                    TotalAmount = offset + 1
                })
                .ToList();
            var previousOrders = Enumerable.Range(0, 7)
                .Select(offset => new Order
                {
                    CreatedOn = from.AddDays(offset - 7).AddHours(10),
                    TotalAmount = 1
                })
                .ToList();

            _orderRepository
                .SetupSequence(repo => repo.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(currentOrders)
                .ReturnsAsync(previousOrders);

            // Act
            var result = await _sut.GetSalesAsync(from, to, MetricsGranularity.Day);

            // Assert
            Assert.Equal(7, result.Points.Count);
            Assert.Equal(28m, result.Total);
            Assert.Equal(7m, result.PreviousTotal);
            Assert.True(result.TrendPercentage > 0);
            Assert.All(result.Points, p => Assert.True(p.Value >= 0));
        }

        [Fact]
        public async Task GetTrafficAsync_ReturnsZeroWhenNoData()
        {
            // Arrange
            var from = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = from.AddDays(6);
            _newsletterRepository
                .Setup(repo => repo.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<NewsletterSubscriber>());
            _orderRepository
                .Setup(repo => repo.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                .ReturnsAsync(new List<Order>());

            // Act
            var result = await _sut.GetTrafficAsync(from, to, MetricsGranularity.Day);

            // Assert
            Assert.Equal(0m, result.Total);
            Assert.Equal(0m, result.PreviousTotal);
            Assert.All(result.Points, p => Assert.Equal(0m, p.Value));
        }
    }
}