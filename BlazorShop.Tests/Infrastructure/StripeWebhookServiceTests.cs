namespace BlazorShop.Tests.Infrastructure
{
    using BlazorShop.Application.Options;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Services;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Moq;

    using Xunit;

    public sealed class StripeWebhookServiceTests
    {
        private readonly Mock<IStripeWebhookEventParser> _parser = new();
        private readonly Mock<IOrderRepository> _orders = new();
        private readonly StripeWebhookService _service;

        public StripeWebhookServiceTests()
        {
            _service = new StripeWebhookService(
                _parser.Object,
                _orders.Object,
                Options.Create(new StripeOptions { WebhookSecret = "whsec_test" }),
                Mock.Of<ILogger<StripeWebhookService>>());
        }

        [Fact]
        public async Task HandleAsync_PaidCheckout_MarksPendingOrderPaid()
        {
            var order = new Order { Id = Guid.NewGuid(), Status = PaymentOrderStatus.PendingPayment };
            SetupEvent("checkout.session.completed", order.Id, "paid");
            _orders.Setup(repository => repository.GetByIdAsync(order.Id)).ReturnsAsync(order);
            _orders
                .Setup(repository => repository.UpdatePaymentStatusAsync(order.Id, PaymentOrderStatus.Paid))
                .ReturnsAsync(1);

            var result = await _service.HandleAsync("{}", "valid-signature");

            Assert.Equal(StripeWebhookHandlingResult.Processed, result);
            _orders.Verify(
                repository => repository.UpdatePaymentStatusAsync(order.Id, PaymentOrderStatus.Paid),
                Times.Once);
        }

        [Fact]
        public async Task HandleAsync_DuplicatePaidEvent_DoesNotWriteAgain()
        {
            var order = new Order { Id = Guid.NewGuid(), Status = PaymentOrderStatus.Paid };
            SetupEvent("checkout.session.completed", order.Id, "paid");
            _orders.Setup(repository => repository.GetByIdAsync(order.Id)).ReturnsAsync(order);

            var result = await _service.HandleAsync("{}", "valid-signature");

            Assert.Equal(StripeWebhookHandlingResult.Processed, result);
            _orders.Verify(
                repository => repository.UpdatePaymentStatusAsync(It.IsAny<Guid>(), It.IsAny<string>()),
                Times.Never);
        }

        [Theory]
        [InlineData("checkout.session.async_payment_failed", PaymentOrderStatus.PaymentFailed)]
        [InlineData("checkout.session.expired", PaymentOrderStatus.Cancelled)]
        public async Task HandleAsync_FailureEvent_UpdatesPendingOrder(string eventType, string expectedStatus)
        {
            var order = new Order { Id = Guid.NewGuid(), Status = PaymentOrderStatus.PendingPayment };
            SetupEvent(eventType, order.Id, null);
            _orders.Setup(repository => repository.GetByIdAsync(order.Id)).ReturnsAsync(order);
            _orders.Setup(repository => repository.UpdatePaymentStatusAsync(order.Id, expectedStatus)).ReturnsAsync(1);

            var result = await _service.HandleAsync("{}", "valid-signature");

            Assert.Equal(StripeWebhookHandlingResult.Processed, result);
            _orders.Verify(repository => repository.UpdatePaymentStatusAsync(order.Id, expectedStatus), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_FailureEvent_DoesNotDowngradePaidOrder()
        {
            var order = new Order { Id = Guid.NewGuid(), Status = PaymentOrderStatus.Paid };
            SetupEvent("checkout.session.async_payment_failed", order.Id, null);
            _orders.Setup(repository => repository.GetByIdAsync(order.Id)).ReturnsAsync(order);

            var result = await _service.HandleAsync("{}", "valid-signature");

            Assert.Equal(StripeWebhookHandlingResult.Processed, result);
            _orders.Verify(
                repository => repository.UpdatePaymentStatusAsync(It.IsAny<Guid>(), It.IsAny<string>()),
                Times.Never);
        }

        [Fact]
        public async Task HandleAsync_InvalidSignature_IsRejectedBeforeDatabaseAccess()
        {
            _parser
                .Setup(parser => parser.Parse(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new StripeWebhookSignatureException(new InvalidOperationException()));

            var result = await _service.HandleAsync("{}", "invalid-signature");

            Assert.Equal(StripeWebhookHandlingResult.InvalidSignature, result);
            _orders.Verify(repository => repository.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
        }

        private void SetupEvent(string eventType, Guid orderId, string? paymentStatus)
        {
            _parser
                .Setup(parser => parser.Parse("{}", "valid-signature", "whsec_test"))
                .Returns(new StripeWebhookEventData("evt_test", eventType, orderId, paymentStatus));
        }
    }
}
