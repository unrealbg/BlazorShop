namespace BlazorShop.Tests.Infrastructure
{
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Options;
    using BlazorShop.Domain.Entities;
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

            var result = await paymentService.Pay(
                25m,
                [new Product { Id = productId, Name = "Camera", Description = "Mirrorless", Price = 25m }],
                [new ProcessCart { ProductId = productId, Quantity = 1 }]);

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
            SessionCreateOptions? capturedOptions = null;

            sessionService
                .Setup(service => service.CreateAsync(It.IsAny<SessionCreateOptions>(), It.IsAny<CancellationToken>()))
                .Callback<SessionCreateOptions, CancellationToken>((options, _) => capturedOptions = options)
                .ReturnsAsync(new Session { Url = "https://checkout.stripe.com/session/test" });

            var paymentService = CreatePaymentService(sessionService.Object, logger.Object);

            var result = await paymentService.Pay(
                25m,
                [new Product { Id = productId, Name = "Camera", Description = "Mirrorless", Price = 25m }],
                [new ProcessCart { ProductId = productId, Quantity = 1 }]);

            Assert.True(result.Success);
            Assert.Equal("https://checkout.stripe.com/session/test", result.Message);
            Assert.NotNull(capturedOptions);
            Assert.Equal("https://shop.example.com/payment-success", capturedOptions!.SuccessUrl);
            Assert.Equal("https://shop.example.com/payment-cancel", capturedOptions.CancelUrl);
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