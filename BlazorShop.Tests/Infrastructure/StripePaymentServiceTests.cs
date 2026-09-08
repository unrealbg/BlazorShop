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
                .ReturnsAsync(new Session
                {
                    Id = "cs_test",
                    PaymentIntentId = "pi_test",
                    Url = "https://checkout.stripe.com/session/test",
                });

            var paymentService = CreatePaymentService(sessionService.Object, logger.Object);
            var orderId = Guid.NewGuid();

            var initialization = CreateInitialization(orderId, productId, variantId);
            var result = await paymentService.Pay(
                initialization,
                "provider-key");

            Assert.True(result.Success);
            Assert.Equal("https://checkout.stripe.com/session/test", result.RedirectUrl);
            Assert.Equal("cs_test", result.ProviderSessionId);
            Assert.Equal("pi_test", result.ProviderPaymentIntentId);
            Assert.NotNull(capturedOptions);
            Assert.Equal(
                $"https://shop.example.com/payment-success?pm=card&order_id={orderId:D}&reference=STRIPE-TEST-1&session_id={{CHECKOUT_SESSION_ID}}",
                capturedOptions!.SuccessUrl);
            Assert.Equal($"https://shop.example.com/payment-cancel?order_id={orderId:D}", capturedOptions.CancelUrl);
            Assert.Equal(orderId.ToString("D"), capturedOptions.ClientReferenceId);
            Assert.Equal(orderId.ToString("D"), capturedOptions.Metadata["order_id"]);
            Assert.Equal(
                initialization.PaymentTransactionId.ToString("D"),
                capturedOptions.Metadata["payment_transaction_id"]);
            Assert.Equal(orderId.ToString("D"), capturedOptions.PaymentIntentData.Metadata["order_id"]);
            Assert.Equal(
                initialization.PaymentTransactionId.ToString("D"),
                capturedOptions.PaymentIntentData.Metadata["payment_transaction_id"]);
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

        [Fact]
        public async Task Pay_WhenLineTotalDoesNotMatchExpectedAmount_RejectsBeforeStripeCall()
        {
            var sessionService = new Mock<IStripeCheckoutSessionService>();
            var initialization = CreateInitialization(Guid.NewGuid(), Guid.NewGuid()) with
            {
                ExpectedAmountMinor = 2499,
            };
            var paymentService = CreatePaymentService(
                sessionService.Object,
                Mock.Of<ILogger<StripePaymentService>>());

            var result = await paymentService.Pay(initialization, "provider-key");

            Assert.False(result.Success);
            Assert.Equal(PaymentInitializationFailureKind.Definitive, result.FailureKind);
            sessionService.Verify(service => service.CreateAsync(
                It.IsAny<SessionCreateOptions>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
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

        [Theory]
        [InlineData(HttpStatusCode.BadRequest, "invalid_request_error")]
        [InlineData(HttpStatusCode.Unauthorized, "authentication_error")]
        [InlineData(HttpStatusCode.Forbidden, "permission_error")]
        public async Task Pay_WhenCurrentStripeRequestIsDeterministicallyRejected_ReturnsDefinitive(
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

        [Fact]
        public async Task RecoverAsync_WhenProviderConfirmsPaid_ReturnsPaidWithoutExpiration()
        {
            var sessionService = new Mock<IStripeCheckoutSessionService>();
            var initialization = CreateInitialization(Guid.NewGuid(), Guid.NewGuid());
            sessionService.Setup(service => service.GetAsync("cs_recovery", It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateRecoverySession(initialization, "paid", "complete"));
            var paymentService = CreatePaymentService(
                sessionService.Object,
                Mock.Of<ILogger<StripePaymentService>>());

            var result = await paymentService.RecoverAsync(
                new StripePaymentRecoveryRequest(initialization, "cs_recovery", "pi_recovery"));

            Assert.Equal(StripePaymentRecoveryOutcome.Paid, result.Outcome);
            sessionService.Verify(service => service.ExpireAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RecoverAsync_WhenOpenSessionExpires_ReReadsAndReturnsConfirmedExpiration()
        {
            var sessionService = new Mock<IStripeCheckoutSessionService>();
            var initialization = CreateInitialization(Guid.NewGuid(), Guid.NewGuid());
            sessionService.SetupSequence(service => service.GetAsync(
                    "cs_recovery",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateRecoverySession(initialization, "unpaid", "open"))
                .ReturnsAsync(CreateRecoverySession(initialization, "unpaid", "expired"));
            sessionService.Setup(service => service.ExpireAsync(
                    "cs_recovery",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateRecoverySession(initialization, "unpaid", "expired"));
            var paymentService = CreatePaymentService(
                sessionService.Object,
                Mock.Of<ILogger<StripePaymentService>>());

            var result = await paymentService.RecoverAsync(
                new StripePaymentRecoveryRequest(initialization, "cs_recovery", "pi_recovery"));

            Assert.Equal(StripePaymentRecoveryOutcome.Expired, result.Outcome);
            sessionService.Verify(service => service.GetAsync(
                "cs_recovery",
                It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task RecoverAsync_WhenExpirationTimesOutAndPaymentCompletes_ReReadReturnsPaid()
        {
            var sessionService = new Mock<IStripeCheckoutSessionService>();
            var initialization = CreateInitialization(Guid.NewGuid(), Guid.NewGuid());
            sessionService.SetupSequence(service => service.GetAsync(
                    "cs_recovery",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateRecoverySession(initialization, "unpaid", "open"))
                .ReturnsAsync(CreateRecoverySession(initialization, "paid", "complete"));
            sessionService.Setup(service => service.ExpireAsync(
                    "cs_recovery",
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TimeoutException("Expiration response was lost."));
            var paymentService = CreatePaymentService(
                sessionService.Object,
                Mock.Of<ILogger<StripePaymentService>>());

            var result = await paymentService.RecoverAsync(
                new StripePaymentRecoveryRequest(initialization, "cs_recovery", "pi_recovery"));

            Assert.Equal(StripePaymentRecoveryOutcome.Paid, result.Outcome);
        }

        [Fact]
        public async Task RecoverAsync_WhenExpirationAndFinalLookupTimeOut_RemainsUnresolved()
        {
            var sessionService = new Mock<IStripeCheckoutSessionService>();
            var initialization = CreateInitialization(Guid.NewGuid(), Guid.NewGuid());
            sessionService.SetupSequence(service => service.GetAsync(
                    "cs_recovery",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateRecoverySession(initialization, "unpaid", "open"))
                .ThrowsAsync(new TimeoutException("Final provider lookup timed out."));
            sessionService.Setup(service => service.ExpireAsync(
                    "cs_recovery",
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new TimeoutException("Expiration response was lost."));
            var paymentService = CreatePaymentService(
                sessionService.Object,
                Mock.Of<ILogger<StripePaymentService>>());

            var result = await paymentService.RecoverAsync(
                new StripePaymentRecoveryRequest(initialization, "cs_recovery", "pi_recovery"));

            Assert.Equal(StripePaymentRecoveryOutcome.Unresolved, result.Outcome);
        }

        [Fact]
        public async Task RecoverAsync_WhenProviderIdentityContradictsSnapshot_RemainsUnresolved()
        {
            var sessionService = new Mock<IStripeCheckoutSessionService>();
            var initialization = CreateInitialization(Guid.NewGuid(), Guid.NewGuid());
            var contradictory = CreateRecoverySession(initialization, "unpaid", "expired");
            contradictory.AmountTotal = initialization.ExpectedAmountMinor + 1;
            sessionService.Setup(service => service.GetAsync("cs_recovery", It.IsAny<CancellationToken>()))
                .ReturnsAsync(contradictory);
            var paymentService = CreatePaymentService(
                sessionService.Object,
                Mock.Of<ILogger<StripePaymentService>>());

            var result = await paymentService.RecoverAsync(
                new StripePaymentRecoveryRequest(initialization, "cs_recovery", "pi_recovery"));

            Assert.Equal(StripePaymentRecoveryOutcome.Unresolved, result.Outcome);
        }

        private static StripeCheckoutInitialization CreateInitialization(
            Guid orderId,
            Guid productId,
            Guid? variantId = null) => new(
                2,
                orderId,
                "STRIPE-TEST-1",
                Guid.NewGuid(),
                variantId.HasValue ? 7990 : 2500,
                "EUR",
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

        private static Session CreateRecoverySession(
            StripeCheckoutInitialization initialization,
            string paymentStatus,
            string status) => new()
            {
                Id = "cs_recovery",
                PaymentIntentId = "pi_recovery",
                ClientReferenceId = initialization.OrderId.ToString("D"),
                AmountTotal = initialization.ExpectedAmountMinor,
                Currency = initialization.Currency.ToLowerInvariant(),
                PaymentStatus = paymentStatus,
                Status = status,
                Metadata = new Dictionary<string, string>
                {
                    ["order_id"] = initialization.OrderId.ToString("D"),
                    ["payment_transaction_id"] = initialization.PaymentTransactionId.ToString("D"),
                },
            };

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
