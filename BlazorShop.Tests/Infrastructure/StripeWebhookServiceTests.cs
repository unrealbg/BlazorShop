namespace BlazorShop.Tests.Infrastructure
{
    using BlazorShop.Application.Options;
    using BlazorShop.Infrastructure.Services;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Moq;

    using Xunit;

    public sealed class StripeWebhookServiceTests
    {
        private readonly Mock<IStripeWebhookEventParser> _parser = new();
        private readonly Mock<IStripePaymentReconciliationService> _reconciliation = new();
        private readonly StripeWebhookService _service;

        public StripeWebhookServiceTests()
        {
            _service = new StripeWebhookService(
                _parser.Object,
                _reconciliation.Object,
                Options.Create(new StripeOptions { WebhookSecret = "whsec_test" }),
                Mock.Of<ILogger<StripeWebhookService>>());
        }

        [Fact]
        public async Task HandleAsync_PaidCheckout_MarksPendingOrderPaid()
        {
            var stripeEvent = SetupEvent("checkout.session.completed", "paid");
            _reconciliation.Setup(service => service.ReconcileAsync(
                    stripeEvent,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(StripePaymentReconciliationOutcome.Processed);

            var result = await _service.HandleAsync("{}", "valid-signature");

            Assert.Equal(StripeWebhookHandlingResult.Processed, result);
            _reconciliation.Verify(
                service => service.ReconcileAsync(stripeEvent, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task HandleAsync_DuplicatePaidEvent_IsTreatedAsProcessed()
        {
            var stripeEvent = SetupEvent("checkout.session.completed", "paid");
            _reconciliation.Setup(service => service.ReconcileAsync(
                    stripeEvent,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(StripePaymentReconciliationOutcome.Duplicate);

            var result = await _service.HandleAsync("{}", "valid-signature");

            Assert.Equal(StripeWebhookHandlingResult.Duplicate, result);
            _reconciliation.VerifyAll();
        }

        [Theory]
        [InlineData("checkout.session.async_payment_failed")]
        [InlineData("checkout.session.expired")]
        public async Task HandleAsync_FailureEvent_IsReconciled(string eventType)
        {
            var stripeEvent = SetupEvent(eventType, null);
            _reconciliation.Setup(service => service.ReconcileAsync(
                    stripeEvent,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(StripePaymentReconciliationOutcome.Processed);

            var result = await _service.HandleAsync("{}", "valid-signature");

            Assert.Equal(StripeWebhookHandlingResult.Processed, result);
            _reconciliation.Verify(
                service => service.ReconcileAsync(stripeEvent, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task HandleAsync_InvalidSignature_IsRejectedBeforeDatabaseAccess()
        {
            _parser
                .Setup(parser => parser.Parse(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new StripeWebhookSignatureException(new InvalidOperationException()));

            var result = await _service.HandleAsync("{}", "invalid-signature");

            Assert.Equal(StripeWebhookHandlingResult.InvalidSignature, result);
            _reconciliation.Verify(
                service => service.ReconcileAsync(
                    It.IsAny<StripeWebhookEventData>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private StripeWebhookEventData SetupEvent(string eventType, string? paymentStatus)
        {
            var orderId = Guid.NewGuid();
            var paymentTransactionId = Guid.NewGuid();
            var stripeEvent = new StripeWebhookEventData(
                "evt_test",
                eventType,
                DateTime.UtcNow,
                "cs_test",
                "pi_test",
                orderId,
                orderId.ToString("D"),
                paymentTransactionId,
                paymentStatus,
                1000,
                "eur",
                "complete");
            _parser
                .Setup(parser => parser.Parse("{}", "valid-signature", "whsec_test"))
                .Returns(stripeEvent);
            return stripeEvent;
        }
    }
}
