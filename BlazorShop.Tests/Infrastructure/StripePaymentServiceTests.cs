namespace BlazorShop.Tests.Infrastructure
{
    using System.Net;

    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Infrastructure.Services;

    using Microsoft.Extensions.Logging;

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
                .Setup(service => service.CreateAsync(
                    It.IsAny<SessionCreateOptions>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Sensitive Stripe error details"));

            var paymentService = CreatePaymentService(sessionService.Object, logger.Object);
            var orderId = Guid.NewGuid();

            var result = await paymentService.Pay(
                CreateInitialization(orderId, productId),
                "provider-key");

            Assert.False(result.Success);
            Assert.Contains("uncertain", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(PaymentInitializationFailureKind.Ambiguous, result.FailureKind);
            Assert.DoesNotContain("Sensitive Stripe error details", result.ErrorMessage, StringComparison.Ordinal);
            Assert.Null(result.RedirectUrl);
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
                .Setup(service => service.CreateAsync(
                    It.IsAny<SessionCreateOptions>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<SessionCreateOptions, string, CancellationToken>((options, _, _) => capturedOptions = options)
                .ReturnsAsync(new Session { Url = "https://checkout.stripe.com/session/test" });

            var paymentService = CreatePaymentService(sessionService.Object, logger.Object);
            var orderId = Guid.NewGuid();

            var result = await paymentService.Pay(
                CreateInitialization(orderId, productId, variantId),
                "provider-key");

            Assert.True(result.Success);
            Assert.Equal("https://checkout.stripe.com/session/test", result.RedirectUrl);
            Assert.NotNull(capturedOptions);
            Assert.Equal(
                $"https://shop.example.com/payment-success?pm=card&order_id={orderId:D}&reference=STRIPE-TEST-1&session_id={{CHECKOUT_SESSION_ID}}",
                capturedOptions!.SuccessUrl);
            Assert.Equal($"https://shop.example.com/payment-cancel?order_id={orderId:D}", capturedOptions.CancelUrl);
            Assert.Equal(orderId.ToString("D"), capturedOptions.ClientReferenceId);
            Assert.Equal(orderId.ToString("D"), capturedOptions.Metadata["order_id"]);
            Assert.Equal(orderId.ToString("D"), capturedOptions.PaymentIntentData.Metadata["order_id"]);
            Assert.Equal(["card"], capturedOptions.PaymentMethodTypes);
            Assert.Equal("payment", capturedOptions.Mode);
            var lineItem = Assert.Single(capturedOptions.LineItems);
            Assert.Equal(3995, lineItem.PriceData.UnitAmount);
            Assert.Equal(2, lineItem.Quantity);
            Assert.Equal("Camera", lineItem.PriceData.ProductData.Name);
            Assert.Equal("SKU: CAM-BLK, Size: ShoesEU 42, Color: Black", lineItem.PriceData.ProductData.Description);
            sessionService.Verify(service => service.CreateAsync(
                It.IsAny<SessionCreateOptions>(),
                "provider-key",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(HttpStatusCode.Conflict, "idempotency_error")]
        [InlineData(HttpStatusCode.TooManyRequests, "rate_limit_error")]
        [InlineData(HttpStatusCode.InternalServerError, "api_error")]
        [InlineData(HttpStatusCode.BadGateway, "api_error")]
        public async Task Pay_WhenStripeFailureIsRetryable_ReturnsAmbiguous(
            HttpStatusCode statusCode,
            string errorType)
        {
            var sessionService = new Mock<IStripeCheckoutSessionService>();
            sessionService.Setup(service => service.CreateAsync(
                    It.IsAny<SessionCreateOptions>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Stripe.StripeException(
                    statusCode,
                    new Stripe.StripeError { Type = errorType },
                    "retryable"));
            var paymentService = CreatePaymentService(
                sessionService.Object,
                Mock.Of<ILogger<StripePaymentService>>());

            var result = await paymentService.Pay(
                CreateInitialization(Guid.NewGuid(), Guid.NewGuid()),
                "provider-key");

            Assert.False(result.Success);
            Assert.Equal(PaymentInitializationFailureKind.Ambiguous, result.FailureKind);
        }

        [Fact]
        public async Task Pay_WhenStripeDeterministicallyRejectsRequest_ReturnsDefinitive()
        {
            var sessionService = new Mock<IStripeCheckoutSessionService>();
            sessionService.Setup(service => service.CreateAsync(
                    It.IsAny<SessionCreateOptions>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Stripe.StripeException(
                    HttpStatusCode.BadRequest,
                    new Stripe.StripeError { Type = "invalid_request_error" },
                    "invalid request"));
            var paymentService = CreatePaymentService(
                sessionService.Object,
                Mock.Of<ILogger<StripePaymentService>>());

            var result = await paymentService.Pay(
                CreateInitialization(Guid.NewGuid(), Guid.NewGuid()),
                "provider-key");

            Assert.False(result.Success);
            Assert.Equal(PaymentInitializationFailureKind.Definitive, result.FailureKind);
        }

        private static StripeCheckoutInitialization CreateInitialization(
            Guid orderId,
            Guid productId,
            Guid? variantId = null) => new(
                1,
                orderId,
                "STRIPE-TEST-1",
                ["card"],
                "payment",
                [
                    new StripeCheckoutLineItem(
                        productId,
                        variantId,
                        "Camera",
                        variantId.HasValue ? "SKU: CAM-BLK, Size: ShoesEU 42, Color: Black" : null,
                        variantId.HasValue ? 2 : 1,
                        variantId.HasValue ? 3995 : 2500,
                        "eur"),
                ],
                $"https://shop.example.com/payment-success?pm=card&order_id={orderId:D}&reference=STRIPE-TEST-1&session_id={{CHECKOUT_SESSION_ID}}",
                $"https://shop.example.com/payment-cancel?order_id={orderId:D}");

        private static StripePaymentService CreatePaymentService(
            IStripeCheckoutSessionService sessionService,
            ILogger<StripePaymentService> logger)
        {
            return new StripePaymentService(
                sessionService,
                logger);
        }
    }
}
