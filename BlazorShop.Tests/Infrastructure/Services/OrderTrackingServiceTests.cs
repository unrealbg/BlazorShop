namespace BlazorShop.Tests.Infrastructure.Services
{
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Services;

    using Microsoft.EntityFrameworkCore;

    using Moq;

    using Xunit;

    public class OrderTrackingServiceTests
    {
        [Fact]
        public async Task UpdateTrackingAsync_ReturnsFalse_WhenOrderDoesNotExist()
        {
            await using var context = CreateContext();
            var emailService = new Mock<IEmailService>();
            var service = new OrderTrackingService(context, emailService.Object);

            var result = await service.UpdateTrackingAsync(Guid.NewGuid(), "UPS", "1Z123", "https://example.com/track");

            Assert.False(result);
            emailService.Verify(email => email.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UpdateShippingStatusAsync_ReturnsTrue_WhenOrderExists()
        {
            await using var context = CreateContext();
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = "user-1",
                Reference = "order-1",
                ShippingStatus = "PendingShipment",
            };

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            var emailService = new Mock<IEmailService>();
            var service = new OrderTrackingService(context, emailService.Object);
            var shippedOn = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);

            var result = await service.UpdateShippingStatusAsync(order.Id, "Shipped", shippedOn);

            Assert.True(result);

            var savedOrder = await context.Orders.SingleAsync(saved => saved.Id == order.Id);
            Assert.Equal("Shipped", savedOrder.ShippingStatus);
            Assert.Equal(shippedOn, savedOrder.ShippedOn);
            Assert.NotNull(savedOrder.LastTrackingUpdate);
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"order-tracking-service-tests-{Guid.NewGuid()}")
                .Options;

            return new AppDbContext(options);
        }
    }
}