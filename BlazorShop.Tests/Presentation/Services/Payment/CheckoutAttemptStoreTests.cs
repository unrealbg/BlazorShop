namespace BlazorShop.Tests.Presentation.Services.Payment
{
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Http.Json;

    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.BrowserStorage.Contracts;
    using BlazorShop.Web.Shared.CookieStorage.Contracts;
    using BlazorShop.Web.Shared.Helper;
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Services;

    using Moq;

    using Xunit;

    public sealed class CheckoutAttemptStoreTests
    {
        [Fact]
        public async Task SameIntentAndBrowserReload_ReuseKeyWhileMaterialChangeCreatesNewKey()
        {
            string? sessionValue = null;
            var storage = new Mock<IBrowserSessionStorageService>();
            storage.Setup(service => service.GetAsync(It.IsAny<string>()))
                .ReturnsAsync(() => sessionValue);
            storage.Setup(service => service.SetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((_, value) => sessionValue = value)
                .Returns(Task.CompletedTask);
            var productId = Guid.NewGuid();
            var checkout = CreateCheckout(Guid.NewGuid(), productId, 1);
            var firstStore = new CheckoutAttemptStore(storage.Object);

            var first = await firstStore.GetOrCreateAsync(checkout);
            var afterReload = await new CheckoutAttemptStore(storage.Object).GetOrCreateAsync(checkout);
            var changedQuantity = await firstStore.GetOrCreateAsync(
                CreateCheckout(checkout.PaymentMethodId, productId, 2));
            var changedPayment = await firstStore.GetOrCreateAsync(
                CreateCheckout(Guid.NewGuid(), productId, 2));

            Assert.Equal(first.IdempotencyKey, afterReload.IdempotencyKey);
            Assert.NotEqual(first.IdempotencyKey, changedQuantity.IdempotencyKey);
            Assert.NotEqual(changedQuantity.IdempotencyKey, changedPayment.IdempotencyKey);
        }

        [Fact]
        public async Task EquivalentReorderedAndGroupedIntent_ReusesClientAttempt()
        {
            string? sessionValue = null;
            var storage = CreateStorage(value => sessionValue = value, () => sessionValue);
            var firstProduct = Guid.NewGuid();
            var secondProduct = Guid.NewGuid();
            var paymentMethod = Guid.NewGuid();
            var store = new CheckoutAttemptStore(storage.Object);
            var split = new Checkout
            {
                PaymentMethodId = paymentMethod,
                Carts =
                [
                    new CartLineRequest(firstProduct, null, 1),
                    new CartLineRequest(secondProduct, null, 1),
                    new CartLineRequest(firstProduct, null, 2),
                ],
            };
            var grouped = new Checkout
            {
                PaymentMethodId = paymentMethod,
                Carts =
                [
                    new CartLineRequest(secondProduct, null, 1),
                    new CartLineRequest(firstProduct, null, 3),
                ],
            };

            var first = await store.GetOrCreateAsync(split);
            var equivalent = await store.GetOrCreateAsync(grouped);

            Assert.Equal(first.IdempotencyKey, equivalent.IdempotencyKey);
            Assert.Equal(first.IntentSignature, equivalent.IntentSignature);
        }

        [Fact]
        public async Task ConcurrentSameIntentGetOrCreate_ReturnsOneKey()
        {
            string? sessionValue = null;
            var storage = new Mock<IBrowserSessionStorageService>();
            storage.Setup(service => service.GetAsync(It.IsAny<string>()))
                .Returns(async () =>
                {
                    await Task.Delay(50);
                    return sessionValue;
                });
            storage.Setup(service => service.SetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((_, value) => sessionValue = value)
                .Returns(Task.CompletedTask);
            var store = new CheckoutAttemptStore(storage.Object);
            var checkout = CreateCheckout(Guid.NewGuid(), Guid.NewGuid(), 1);

            var attempts = await Task.WhenAll(
                store.GetOrCreateAsync(checkout),
                store.GetOrCreateAsync(checkout));

            Assert.Equal(attempts[0].IdempotencyKey, attempts[1].IdempotencyKey);
            storage.Verify(
                service => service.SetAsync(It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
        }

        [Fact]
        public async Task SuccessfulCheckoutThenClientCrash_SameIntentReusesCompletedKeyAfterReload()
        {
            string? sessionValue = null;
            var storage = CreateStorage(value => sessionValue = value, () => sessionValue);
            var checkout = CreateCheckout(Guid.NewGuid(), Guid.NewGuid(), 1);
            var observed = new ConcurrentQueue<string>();
            var client = CreateSuccessfulCheckoutClient(observed);
            var clients = new Mock<IHttpClientHelper>();
            clients.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);
            var service = new CartService(
                clients.Object,
                new ApiCallHelper(),
                new CheckoutAttemptStore(storage.Object));

            Assert.True((await service.Checkout(checkout)).Success);
            var afterReload = await new CheckoutAttemptStore(storage.Object).GetOrCreateAsync(checkout);

            Assert.Equal(Assert.Single(observed), afterReload.IdempotencyKey.ToString("D"));
            storage.Verify(service => service.RemoveAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SuccessfulCheckoutWhenCartCleanupFails_SameIntentStillReusesCompletedKey()
        {
            string? sessionValue = null;
            var storage = CreateStorage(value => sessionValue = value, () => sessionValue);
            var cookieStorage = new Mock<IBrowserCookieStorageService>();
            cookieStorage.Setup(service => service.RemoveAsync(Constant.Cart.Name))
                .ThrowsAsync(new InvalidOperationException("Cart cookie cleanup failed"));
            var checkout = CreateCheckout(Guid.NewGuid(), Guid.NewGuid(), 1);
            var observed = new ConcurrentQueue<string>();
            var clients = new Mock<IHttpClientHelper>();
            clients.Setup(helper => helper.GetPrivateClientAsync())
                .ReturnsAsync(CreateSuccessfulCheckoutClient(observed));
            var store = new CheckoutAttemptStore(storage.Object);
            var service = new CartService(clients.Object, new ApiCallHelper(), store);

            Assert.True((await service.Checkout(checkout)).Success);
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => cookieStorage.Object.RemoveAsync(Constant.Cart.Name));
            var retained = await store.GetOrCreateAsync(checkout);

            Assert.Equal(Assert.Single(observed), retained.IdempotencyKey.ToString("D"));
            storage.Verify(service => service.RemoveAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SuccessfulCheckoutThenMaterialIntentChange_GeneratesNewKeyNaturally()
        {
            string? sessionValue = null;
            var storage = CreateStorage(value => sessionValue = value, () => sessionValue);
            var productId = Guid.NewGuid();
            var checkout = CreateCheckout(Guid.NewGuid(), productId, 1);
            var observed = new ConcurrentQueue<string>();
            var clients = new Mock<IHttpClientHelper>();
            clients.Setup(helper => helper.GetPrivateClientAsync())
                .ReturnsAsync(CreateSuccessfulCheckoutClient(observed));
            var store = new CheckoutAttemptStore(storage.Object);
            var service = new CartService(clients.Object, new ApiCallHelper(), store);

            Assert.True((await service.Checkout(checkout)).Success);
            var changed = await store.GetOrCreateAsync(
                CreateCheckout(checkout.PaymentMethodId, productId, 2));

            Assert.NotEqual(Assert.Single(observed), changed.IdempotencyKey.ToString("D"));
            storage.Verify(service => service.SetAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task AmbiguousTransportFailure_KeepsSamePendingAttempt()
        {
            var key = Guid.NewGuid();
            var checkout = CreateCheckout(Guid.NewGuid(), Guid.NewGuid(), 1);
            var attempts = new Mock<BlazorShop.Web.Shared.Services.Contracts.ICheckoutAttemptStore>();
            attempts.Setup(store => store.GetOrCreateAsync(checkout))
                .ReturnsAsync(new CheckoutAttempt(key, "signature"));
            var client = new HttpClient(new AsyncStubHandler((_, _) =>
                throw new HttpRequestException("Response lost")))
            {
                BaseAddress = new Uri("https://shop.test/api/"),
            };
            var clients = new Mock<IHttpClientHelper>();
            clients.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);
            var service = new CartService(clients.Object, new ApiCallHelper(), attempts.Object);

            await Assert.ThrowsAsync<HttpRequestException>(() => service.Checkout(checkout));
            await Assert.ThrowsAsync<HttpRequestException>(() => service.Checkout(checkout));

            attempts.Verify(store => store.GetOrCreateAsync(checkout), Times.Exactly(2));
            attempts.Verify(store => store.ClearAsync(It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task Timeout_KeepsSamePendingAttempt()
        {
            string? sessionValue = null;
            var storage = CreateStorage(value => sessionValue = value, () => sessionValue);
            var store = new CheckoutAttemptStore(storage.Object);
            var checkout = CreateCheckout(Guid.NewGuid(), Guid.NewGuid(), 1);
            var observed = new ConcurrentQueue<string>();
            var client = new HttpClient(new AsyncStubHandler((request, _) =>
            {
                observed.Enqueue(Assert.Single(request.Headers.GetValues("Idempotency-Key")));
                throw new TaskCanceledException("Timed out");
            }))
            {
                BaseAddress = new Uri("https://shop.test/api/"),
            };
            var clients = new Mock<IHttpClientHelper>();
            clients.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);
            var service = new CartService(clients.Object, new ApiCallHelper(), store);

            await Assert.ThrowsAsync<TaskCanceledException>(() => service.Checkout(checkout));
            await Assert.ThrowsAsync<TaskCanceledException>(() => service.Checkout(checkout));

            Assert.Equal(2, observed.Count);
            Assert.Single(observed.Distinct());
        }

        [Fact]
        public async Task InProgressConflict_KeepsSamePendingAttempt()
        {
            string? sessionValue = null;
            var storage = CreateStorage(value => sessionValue = value, () => sessionValue);
            var store = new CheckoutAttemptStore(storage.Object);
            var checkout = CreateCheckout(Guid.NewGuid(), Guid.NewGuid(), 1);
            var observed = new ConcurrentQueue<string>();
            var client = new HttpClient(new AsyncStubHandler((request, _) =>
            {
                observed.Enqueue(Assert.Single(request.Headers.GetValues("Idempotency-Key")));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict)
                {
                    Content = JsonContent.Create(new ServiceResponse<CheckoutResult>(
                        false,
                        "Checkout is still processing. Retry with the same Idempotency-Key.")
                    {
                        ResponseType = ServiceResponseType.Conflict,
                    }),
                });
            }))
            {
                BaseAddress = new Uri("https://shop.test/api/"),
            };
            var clients = new Mock<IHttpClientHelper>();
            clients.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);
            var service = new CartService(clients.Object, new ApiCallHelper(), store);

            await service.Checkout(checkout);
            await service.Checkout(checkout);

            Assert.Equal(2, observed.Count);
            Assert.Single(observed.Distinct());
        }

        [Fact]
        public async Task TerminalFailure_ClearsAttemptSoNextSubmitUsesFreshKey()
        {
            string? sessionValue = null;
            var storage = CreateStorage(value => sessionValue = value, () => sessionValue);
            storage.Setup(service => service.RemoveAsync(It.IsAny<string>()))
                .Callback<string>(_ => sessionValue = null)
                .Returns(Task.CompletedTask);
            var store = new CheckoutAttemptStore(storage.Object);
            var checkout = CreateCheckout(Guid.NewGuid(), Guid.NewGuid(), 1);
            var observed = new ConcurrentQueue<string>();
            var client = new HttpClient(new AsyncStubHandler((request, _) =>
            {
                observed.Enqueue(Assert.Single(request.Headers.GetValues("Idempotency-Key")));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = JsonContent.Create(new ServiceResponse<CheckoutResult>(
                        false,
                        "Provider rejected checkout")
                    {
                        ResponseType = ServiceResponseType.ValidationError,
                    }),
                });
            }))
            {
                BaseAddress = new Uri("https://shop.test/api/"),
            };
            var clients = new Mock<IHttpClientHelper>();
            clients.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);
            var service = new CartService(clients.Object, new ApiCallHelper(), store);

            await service.Checkout(checkout);
            await service.Checkout(checkout);

            Assert.Equal(2, observed.Count);
            Assert.Equal(2, observed.Distinct().Count());
            storage.Verify(service => service.RemoveAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task ConcurrentRequests_UseIsolatedPerRequestHeaders()
        {
            var firstCheckout = CreateCheckout(Guid.NewGuid(), Guid.NewGuid(), 1);
            var secondCheckout = CreateCheckout(Guid.NewGuid(), Guid.NewGuid(), 1);
            var firstKey = Guid.NewGuid();
            var secondKey = Guid.NewGuid();
            var attempts = new Mock<BlazorShop.Web.Shared.Services.Contracts.ICheckoutAttemptStore>();
            attempts.Setup(store => store.GetOrCreateAsync(firstCheckout))
                .ReturnsAsync(new CheckoutAttempt(firstKey, "first"));
            attempts.Setup(store => store.GetOrCreateAsync(secondCheckout))
                .ReturnsAsync(new CheckoutAttempt(secondKey, "second"));
            var observed = new ConcurrentBag<string>();
            var handler = new AsyncStubHandler(async (request, cancellationToken) =>
            {
                observed.Add(Assert.Single(request.Headers.GetValues("Idempotency-Key")));
                await Task.Delay(50, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new ServiceResponse<CheckoutResult>(true)
                    {
                        Payload = new CheckoutResult(
                            Guid.NewGuid(),
                            "TEST",
                            CheckoutStatus.Confirmed,
                            CheckoutPaymentKind.CashOnDelivery),
                    }),
                };
            });
            var client = new HttpClient(handler) { BaseAddress = new Uri("https://shop.test/api/") };
            var clients = new Mock<IHttpClientHelper>();
            clients.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);
            var service = new CartService(clients.Object, new ApiCallHelper(), attempts.Object);

            await Task.WhenAll(service.Checkout(firstCheckout), service.Checkout(secondCheckout));

            Assert.Equal(
                new[] { firstKey.ToString("D"), secondKey.ToString("D") }.Order(StringComparer.Ordinal),
                observed.Order(StringComparer.Ordinal));
            Assert.False(client.DefaultRequestHeaders.Contains("Idempotency-Key"));
            attempts.Verify(store => store.ClearAsync(firstKey), Times.Never);
            attempts.Verify(store => store.ClearAsync(secondKey), Times.Never);
        }

        private static HttpClient CreateSuccessfulCheckoutClient(ConcurrentQueue<string> observed)
        {
            return new HttpClient(new AsyncStubHandler((request, _) =>
            {
                observed.Enqueue(Assert.Single(request.Headers.GetValues("Idempotency-Key")));
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new ServiceResponse<CheckoutResult>(true)
                    {
                        Payload = new CheckoutResult(
                            Guid.NewGuid(),
                            "TEST",
                            CheckoutStatus.Confirmed,
                            CheckoutPaymentKind.CashOnDelivery),
                    }),
                });
            }))
            {
                BaseAddress = new Uri("https://shop.test/api/"),
            };
        }

        private static Mock<IBrowserSessionStorageService> CreateStorage(
            Action<string> write,
            Func<string?> read)
        {
            var storage = new Mock<IBrowserSessionStorageService>();
            storage.Setup(service => service.GetAsync(It.IsAny<string>())).ReturnsAsync(read);
            storage.Setup(service => service.SetAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string>((_, value) => write(value))
                .Returns(Task.CompletedTask);
            storage.Setup(service => service.RemoveAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);
            return storage;
        }

        private static Checkout CreateCheckout(Guid paymentMethodId, Guid productId, int quantity) => new()
        {
            PaymentMethodId = paymentMethodId,
            Carts = [new CartLineRequest(productId, null, quantity)],
        };

        private sealed class AsyncStubHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

            public AsyncStubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
            {
                _handler = handler;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken) => _handler(request, cancellationToken);
        }
    }
}
