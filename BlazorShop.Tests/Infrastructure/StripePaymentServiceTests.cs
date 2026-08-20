namespace BlazorShop.Tests.Infrastructure
{
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Options;
    using BlazorShop.Infrastructure.Services;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Moq;

    using Stripe.Checkout;

    using Xunit;

    public class StripePaymentServiceTests
    {
        [Fact]
        public async Task Pay_WhenCheckoutSessionCreationFails_ReturnsGenericMessage()
        {
            var sessionService = new Mock<IStripeCheckoutSessionService>();
            var logger = new Mock<ILogger<StripePaymentService>>();
            var productId = Guid.NewGuid();

            sessionService
                .Setup(service => service.CreateAsync(It.IsAny<SessionCreateOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Sensitive Stripe error details"));

            var paymentService = CreatePaymentService(sessionService.Object, logger.Object);
            var orderId = Guid.NewGuid();

            var result = await paymentService.Pay(
                [new ResolvedCartLine(productId, null, 1, 25m, "Camera", "Mirrorless", null, null, null)],
                orderId);

            Assert.False(result.Success);
            Assert.Equal("Unable to initialize the card payment session. Please try again later.", result.Message);
            Assert.DoesNotContain("Sensitive Stripe error details", result.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Pay_WhenCheckoutSessionCreationSucceeds_ReturnsCheckoutUrl()
        {
            var sessionService = new Mock<IStripeCheckoutSessionService>();
            var logger = new Mock<ILogger<StripePaymentService>>();
            var productId = Guid.NewGuid();
            var variantId = Guid.NewGuid();
            SessionCreateOptions? capturedOptions = null;

            sessionService
                .Setup(service => service.CreateAsync(It.IsAny<SessionCreateOptions>(), It.IsAny<CancellationToken>()))
                .Callback<SessionCreateOptions, CancellationToken>((options, _) => capturedOptions = options)
                .ReturnsAsync(new Session { Url = "https://checkout.stripe.com/session/test" });

            var paymentService = CreatePaymentService(sessionService.Object, logger.Object);
            var orderId = Guid.NewGuid();

            var result = await paymentService.Pay(
                [new ResolvedCartLine(productId, variantId, 2, 39.95m, "Camera", "Mirrorless", "CAM-BLK", "One Size", "Black")],
                orderId);

            Assert.True(result.Success);
            Assert.Equal("https://checkout.stripe.com/session/test", result.Message);
            Assert.NotNull(capturedOptions);
            Assert.Equal(
                "https://shop.example.com/payment-success?pm=card&session_id={CHECKOUT_SESSION_ID}",
                capturedOptions!.SuccessUrl);
            Assert.Equal($"https://shop.example.com/payment-cancel?order_id={orderId:D}", capturedOptions.CancelUrl);
            Assert.Equal(orderId.ToString("D"), capturedOptions.ClientReferenceId);
            Assert.Equal(orderId.ToString("D"), capturedOptions.Metadata["order_id"]);
            Assert.Equal(orderId.ToString("D"), capturedOptions.PaymentIntentData.Metadata["order_id"]);
            var lineItem = Assert.Single(capturedOptions.LineItems);
            Assert.Equal(3995, lineItem.PriceData.UnitAmount);
            Assert.Equal(2, lineItem.Quantity);
            Assert.Equal("Camera", lineItem.PriceData.ProductData.Name);
            Assert.Contains("CAM-BLK", lineItem.PriceData.ProductData.Description);
        }

        private static StripePaymentService CreatePaymentService(
            IStripeCheckoutSessionService sessionService,
            ILogger<StripePaymentService> logger)
        {
            return new StripePaymentService(
                sessionService,
                Options.Create(new ClientAppOptions { BaseUrl = "https://shop.example.com" }),
                logger);
        }
    }
}
