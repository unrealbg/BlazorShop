namespace BlazorShop.Tests.Infrastructure.Services
{
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Payment;
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
                OrderStatus = OrderStatus.Confirmed,
                PaymentStatus = OrderPaymentStatus.Paid,
                PaymentMethod = OrderPaymentMethod.Stripe,
                FulfillmentStatus = FulfillmentStatus.NotStarted,
            };

            context.Orders.Add(order);
            await context.SaveChangesAsync();

            var emailService = new Mock<IEmailService>();
            var service = new OrderTrackingService(context, emailService.Object);
            var shippedOn = new DateTime(2026, 4, 20, 0, 0, 0, DateTimeKind.Utc);

            var result = await service.UpdateShippingStatusAsync(order.Id, "Shipped", shippedOn);

            Assert.True(result);

            var savedOrder = await context.Orders.SingleAsync(saved => saved.Id == order.Id);
            Assert.Equal(FulfillmentStatus.Shipped, savedOrder.FulfillmentStatus);
            Assert.Equal(OrderPaymentStatus.Paid, savedOrder.PaymentStatus);
            Assert.Equal(shippedOn, savedOrder.ShippedOn);
            Assert.NotNull(savedOrder.LastTrackingUpdate);
        }

        [Theory]
        [InlineData(OrderPaymentMethod.Stripe)]
        [InlineData(OrderPaymentMethod.BankTransfer)]
        public async Task UnpaidElectronicOrder_CannotBeFulfilled(OrderPaymentMethod paymentMethod)
        {
            await using var context = CreateContext();
            var order = new Order
            {
                Id = Guid.NewGuid(),
                Reference = "unpaid",
                OrderStatus = OrderStatus.Confirmed,
                PaymentStatus = OrderPaymentStatus.Pending,
                PaymentMethod = paymentMethod,
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            var service = new OrderTrackingService(context, Mock.Of<IEmailService>());

            var result = await service.TransitionFulfillmentAsync(order.Id, "Shipped");

            Assert.Equal(OrderTrackingTransitionOutcome.Conflict, result.Outcome);
            Assert.Equal(FulfillmentStatus.NotStarted, order.FulfillmentStatus);
            Assert.Equal(OrderPaymentStatus.Pending, order.PaymentStatus);
        }

        [Fact]
        public async Task CashOnDeliveryDelivery_DoesNotImplyPayment_AndRepeatPreservesTimestamps()
        {
            await using var context = CreateContext();
            var order = new Order
            {
                Id = Guid.NewGuid(),
                Reference = "cod",
                OrderStatus = OrderStatus.Confirmed,
                PaymentStatus = OrderPaymentStatus.Pending,
                PaymentMethod = OrderPaymentMethod.CashOnDelivery,
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            var service = new OrderTrackingService(context, Mock.Of<IEmailService>());
            var shippedOn = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
            var deliveredOn = shippedOn.AddDays(2);

            Assert.Equal(OrderTrackingTransitionOutcome.Applied,
                (await service.TransitionFulfillmentAsync(order.Id, "Shipped", shippedOn)).Outcome);
            Assert.Equal(OrderTrackingTransitionOutcome.Applied,
                (await service.TransitionFulfillmentAsync(order.Id, "InTransit")).Outcome);
            Assert.Equal(OrderTrackingTransitionOutcome.Applied,
                (await service.TransitionFulfillmentAsync(order.Id, "Delivered", deliveredOn: deliveredOn)).Outcome);
            var lastUpdate = order.LastTrackingUpdate;

            var repeated = await service.TransitionFulfillmentAsync(
                order.Id,
                "Delivered",
                deliveredOn: deliveredOn.AddDays(1));

            Assert.Equal(OrderTrackingTransitionOutcome.AlreadyApplied, repeated.Outcome);
            Assert.Equal(deliveredOn, order.DeliveredOn);
            Assert.Equal(lastUpdate, order.LastTrackingUpdate);
            Assert.Equal(OrderPaymentStatus.Pending, order.PaymentStatus);
            Assert.Equal(OrderStatus.Confirmed, order.OrderStatus);
        }

        [Fact]
        public async Task BackwardFulfillmentTransition_IsRejected()
        {
            await using var context = CreateContext();
            var order = new Order
            {
                Id = Guid.NewGuid(),
                Reference = "backward",
                OrderStatus = OrderStatus.Confirmed,
                PaymentStatus = OrderPaymentStatus.Paid,
                PaymentMethod = OrderPaymentMethod.Stripe,
                FulfillmentStatus = FulfillmentStatus.InTransit,
                ShippedOn = DateTime.UtcNow.AddDays(-1),
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            var service = new OrderTrackingService(context, Mock.Of<IEmailService>());

            var result = await service.TransitionFulfillmentAsync(order.Id, "Shipped");

            Assert.Equal(OrderTrackingTransitionOutcome.Conflict, result.Outcome);
            Assert.Equal(FulfillmentStatus.InTransit, order.FulfillmentStatus);
            Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
        }

        [Theory]
        [InlineData("UnknownStatus")]
        [InlineData("999")]
        public async Task UndefinedFulfillmentStatus_IsAValidationError(string target)
        {
            await using var context = CreateContext();
            var service = new OrderTrackingService(context, Mock.Of<IEmailService>());

            var result = await service.TransitionFulfillmentAsync(Guid.NewGuid(), target);

            Assert.Equal(OrderTrackingTransitionOutcome.ValidationError, result.Outcome);
        }

        [Fact]
        public async Task FutureFulfillmentTimestamp_IsAValidationError()
        {
            await using var context = CreateContext();
            var service = new OrderTrackingService(context, Mock.Of<IEmailService>());

            var result = await service.TransitionFulfillmentAsync(
                Guid.NewGuid(),
                "Shipped",
                DateTime.UtcNow.AddMinutes(10));

            Assert.Equal(OrderTrackingTransitionOutcome.ValidationError, result.Outcome);
        }

        [Fact]
        public async Task DeliveredBeforeOriginalShippedTimestamp_IsAValidationError()
        {
            await using var context = CreateContext();
            var shippedOn = DateTime.UtcNow.AddDays(-1);
            var order = new Order
            {
                Id = Guid.NewGuid(),
                Reference = "invalid-delivery-time",
                OrderStatus = OrderStatus.Confirmed,
                PaymentStatus = OrderPaymentStatus.Paid,
                PaymentMethod = OrderPaymentMethod.Stripe,
                FulfillmentStatus = FulfillmentStatus.InTransit,
                ShippedOn = shippedOn,
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            var service = new OrderTrackingService(context, Mock.Of<IEmailService>());

            var result = await service.TransitionFulfillmentAsync(
                order.Id,
                "Delivered",
                deliveredOn: shippedOn.AddMinutes(-1));

            Assert.Equal(OrderTrackingTransitionOutcome.ValidationError, result.Outcome);
            Assert.Equal(FulfillmentStatus.InTransit, order.FulfillmentStatus);
        }

        [Theory]
        [InlineData(OrderStatus.Cancelled)]
        [InlineData(OrderStatus.Completed)]
        public async Task TerminalOrder_CannotBeFulfilled(OrderStatus orderStatus)
        {
            await using var context = CreateContext();
            var order = new Order
            {
                Id = Guid.NewGuid(),
                Reference = "terminal-order",
                OrderStatus = orderStatus,
                PaymentStatus = OrderPaymentStatus.Paid,
                PaymentMethod = OrderPaymentMethod.Stripe,
            };
            context.Orders.Add(order);
            await context.SaveChangesAsync();
            var service = new OrderTrackingService(context, Mock.Of<IEmailService>());

            var result = await service.TransitionFulfillmentAsync(order.Id, "Shipped");

            Assert.Equal(OrderTrackingTransitionOutcome.Conflict, result.Outcome);
            Assert.Equal(FulfillmentStatus.NotStarted, order.FulfillmentStatus);
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
