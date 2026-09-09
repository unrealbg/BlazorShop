namespace BlazorShop.Tests.Presentation.Services.Payment
{
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Services;
    using BlazorShop.Web.Shared.Services.Contracts;

    using Moq;

    using Xunit;

    public class CartServiceTests
    {
        [Fact]
        public async Task Checkout_ReturnsTypedCheckoutResponse()
        {
            var checkout = new Checkout
            {
                PaymentMethodId = Guid.NewGuid(),
                Carts = [new CartLineRequest(Guid.NewGuid(), null, 1)],
            };
            var idempotencyKey = Guid.NewGuid();
            string? sentKey = null;
            var handler = new StubHttpMessageHandler(request =>
            {
                sentKey = request.Headers.GetValues("Idempotency-Key").Single();
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            });
            var httpClientHelper = new Mock<IHttpClientHelper>();
            httpClientHelper
                .Setup(helper => helper.GetPrivateClientAsync())
                .ReturnsAsync(new HttpClient(handler) { BaseAddress = new Uri("https://shop.test/api/") });
            var checkoutAttemptStore = new Mock<ICheckoutAttemptStore>();
            checkoutAttemptStore
                .Setup(store => store.GetOrCreateAsync(checkout))
                .ReturnsAsync(new CheckoutAttempt(idempotencyKey, "signature"));
            var checkoutResult = new CheckoutResult(
                Guid.NewGuid(),
                "COD-TEST",
                CheckoutStatus.Confirmed,
                CheckoutPaymentKind.CashOnDelivery);
            var apiCallHelper = new Mock<IApiCallHelper>();
            apiCallHelper
                .Setup(helper => helper.GetMutationResponse<CheckoutResult>(
                    It.IsAny<HttpResponseMessage>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new ServiceResponse<CheckoutResult>(true) { Payload = checkoutResult });
            var service = new CartService(
                httpClientHelper.Object,
                apiCallHelper.Object,
                checkoutAttemptStore.Object);

            var result = await service.Checkout(checkout);

            Assert.Same(checkoutResult, result.Payload);
            Assert.Equal(idempotencyKey.ToString("D"), sentKey);
            checkoutAttemptStore.Verify(store => store.ClearAsync(idempotencyKey), Times.Never);
            checkoutAttemptStore.Verify(
                store => store.ClearForIntentAsync(It.IsAny<Checkout>()),
                Times.Never);
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) => Task.FromResult(_handler(request));
        }
    }
}
