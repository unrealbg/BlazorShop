namespace BlazorShop.Tests.Application.Services.Payment
{
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Application.Services.Payment;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Domain.Entities.Payment;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Moq;

    using Xunit;

    public sealed class CheckoutOrchestratorTests
    {
        private readonly Mock<IProductReadRepository> _products = new();
        private readonly Mock<IPaymentMethodService> _paymentMethods = new();
        private readonly Mock<IPaymentService> _payment = new();
        private readonly Mock<IAppUserManager> _users = new();
        private readonly Mock<IInventoryReservationService> _inventory = new();
        private readonly Mock<ICheckoutIdempotencyStore> _idempotency = new();
        private readonly Mock<IPaymentTransactionStore> _paymentTransactions = new();
        private readonly Mock<IStripePaymentStateTransitionService> _stripeTransitions = new();
        private readonly Mock<IOrderRepository> _orders = new();
        private readonly Mock<IEmailService> _email = new();
        private readonly CheckoutOrchestrator _orchestrator;

        public CheckoutOrchestratorTests()
        {
            _products.Setup(repository => repository.GetProductVariantsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, ProductVariant>());
            _products.Setup(repository => repository.GetProductIdsWithVariantsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new HashSet<Guid>());
            _inventory.Setup(service => service.CreateOrderWithInventoryAsync(
                    It.IsAny<Order>(),
                    It.IsAny<InventoryReservationStatus>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InventoryReservationResult(true));
            _users.Setup(manager => manager.GetUserByIdAsync(It.IsAny<string>()))
                .ReturnsAsync(new AppUser { Id = "customer-1", Email = "customer@example.com" });
            _idempotency.Setup(store => store.ClaimAsync(
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((string userId, Guid key, string fingerprint, Guid paymentMethodId, CancellationToken _) =>
                {
                    var ownerId = Guid.NewGuid();
                    return new CheckoutIdempotencyClaim(
                        CheckoutIdempotencyClaimStatus.Acquired,
                        new CheckoutIdempotencyRecord
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            IdempotencyKey = key,
                            RequestFingerprint = fingerprint,
                            PaymentMethodId = paymentMethodId,
                            OrderId = Guid.NewGuid(),
                            OrderReference = $"{(paymentMethodId == PaymentMethodIds.CashOnDelivery ? "COD" : paymentMethodId == PaymentMethodIds.BankTransfer ? "BT" : "STRIPE")}-{Guid.NewGuid():N}",
                            LeaseOwnerId = ownerId,
                        },
                        ownerId);
                });
            _idempotency.Setup(store => store.SetPendingOutcomeAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<PersistedCheckoutOutcome>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _idempotency.Setup(store => store.PrepareProviderInitializationAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<StripeCheckoutInitialization>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid _, StripeCheckoutInitialization initialization, CancellationToken _) =>
                    new CheckoutProviderInitialization(DateTime.UtcNow, initialization));
            _idempotency.Setup(store => store.CompleteAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<PersistedCheckoutOutcome>(),
                    It.IsAny<CheckoutIdempotencyState>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _idempotency.Setup(store => store.ReleaseLeaseAsync(
                    It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _orders.Setup(repository => repository.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Order?)null);
            _paymentTransactions.Setup(store => store.GetOrCreateStripeAsync(
                    It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid orderId, long amount, string currency, CancellationToken _) =>
                    new PaymentTransaction
                    {
                        Id = Guid.NewGuid(),
                        OrderId = orderId,
                        Provider = PaymentProviderNames.Stripe,
                        ExpectedAmountMinor = amount,
                        Currency = currency,
                    });
            _paymentTransactions.Setup(store => store.PersistProviderIdentityAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentProviderIdentityPersistenceResult(
                    PaymentProviderIdentityPersistenceOutcome.Persisted,
                    PaymentTransactionStatus.Pending));
            _stripeTransitions.Setup(service => service.TransitionAsync(
                    It.IsAny<Guid>(), It.IsAny<PaymentTransactionStatus>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, PaymentTransactionStatus target, CancellationToken _) =>
                    new StripePaymentStateTransitionResult(
                        StripePaymentStateTransitionOutcome.Applied,
                        target));

            _orchestrator = new CheckoutOrchestrator(
                _products.Object,
                _paymentMethods.Object,
                _payment.Object,
                _users.Object,
                _inventory.Object,
                _idempotency.Object,
                _paymentTransactions.Object,
                _stripeTransitions.Object,
                _orders.Object,
                _email.Object,
                Options.Create(new BankTransferSettings
                {
                    Iban = "BG00TEST",
                    Beneficiary = "Blazor Shop",
                    BankName = "Test Bank",
                    AdditionalInfo = "Use the order reference.",
                }),
                Options.Create(new ClientAppOptions { BaseUrl = "https://shop.test" }),
                Options.Create(new CheckoutIdempotencyOptions()),
                Options.Create(new CommerceOptions { Currency = "EUR" }),
                Mock.Of<ILogger<CheckoutOrchestrator>>());
        }

        [Fact]
        public void CheckoutContract_ContainsOnlyPaymentSelectionAndCartIntent()
        {
            Assert.Equal(
                ["Carts", "PaymentMethodId"],
                typeof(Checkout).GetProperties().Select(property => property.Name).Order().ToArray());
            Assert.Equal(
                ["ProductId", "Quantity", "VariantId"],
                typeof(CartLineRequest).GetProperties().Select(property => property.Name).Order().ToArray());
        }

        [Fact]
        public async Task CashOnDelivery_CreatesOwnedOrderAndConsumesInventoryBeforeReturning()
        {
            var product = ConfigureProduct(price: 25m, quantity: 3);
            ConfigurePaymentMethod(PaymentMethodIds.CashOnDelivery, "renamed display value");
            Order? createdOrder = null;
            _inventory.Setup(service => service.CreateOrderWithInventoryAsync(
                    It.IsAny<Order>(),
                    InventoryReservationStatus.Consumed,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Order, InventoryReservationStatus, Guid, CancellationToken>((order, _, _, _) => createdOrder = order)
                .ReturnsAsync(new InventoryReservationResult(true));

            var result = await CheckoutAsync(
                PaymentMethodIds.CashOnDelivery,
                new CartLineRequest(product.Id, null, 2));

            Assert.True(result.Success);
            Assert.NotNull(createdOrder);
            Assert.Equal("customer-1", createdOrder!.UserId);
            Assert.StartsWith("COD-", createdOrder.Reference, StringComparison.Ordinal);
            Assert.Equal(50m, createdOrder.TotalAmount);
            Assert.NotNull(result.Payload);
            Assert.Equal(createdOrder.Id, result.Payload!.OrderId);
            Assert.Equal(createdOrder.Reference, result.Payload.OrderReference);
            Assert.Equal(CheckoutStatus.Confirmed, result.Payload.Status);
            Assert.Equal(CheckoutPaymentKind.CashOnDelivery, result.Payload.PaymentKind);
            var line = Assert.Single(createdOrder.Lines);
            Assert.Null(line.ProductVariantId);
            Assert.Equal(product.Name, line.ProductNameSnapshot);
            Assert.Equal(25m, line.UnitPrice);
            Assert.Equal(50m, line.LineTotal);
            _inventory.Verify(service => service.CreateOrderWithInventoryAsync(
                createdOrder,
                InventoryReservationStatus.Consumed,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()), Times.Once);
            VerifyStripeWasNotCalled();
        }

        [Fact]
        public async Task VariantCashOnDelivery_UsesAuthoritativeVariantPriceAndSnapshots()
        {
            var product = ConfigureProduct(price: 40m, quantity: 0, name: "Runner");
            var variant = new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Price = 65m,
                Stock = 4,
                Sku = "RUN-US-10",
                SizeScale = SizeScale.ShoesUS,
                SizeValue = "10",
                Color = "Black",
            };
            _products.Setup(repository => repository.GetProductVariantsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, ProductVariant> { [variant.Id] = variant });
            _products.Setup(repository => repository.GetProductIdsWithVariantsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new HashSet<Guid> { product.Id });
            ConfigurePaymentMethod(PaymentMethodIds.CashOnDelivery, "Cash on Delivery");
            Order? createdOrder = null;
            _inventory.Setup(service => service.CreateOrderWithInventoryAsync(
                    It.IsAny<Order>(),
                    InventoryReservationStatus.Consumed,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Order, InventoryReservationStatus, Guid, CancellationToken>((order, _, _, _) => createdOrder = order)
                .ReturnsAsync(new InventoryReservationResult(true));

            var result = await CheckoutAsync(
                PaymentMethodIds.CashOnDelivery,
                new CartLineRequest(product.Id, variant.Id, 2));

            Assert.True(result.Success);
            var line = Assert.Single(createdOrder!.Lines);
            Assert.Equal(variant.Id, line.ProductVariantId);
            Assert.Equal("RUN-US-10", line.SkuSnapshot);
            Assert.Equal("ShoesUS", line.SizeScaleSnapshot);
            Assert.Equal("10", line.SizeValueSnapshot);
            Assert.Equal("Black", line.ColorSnapshot);
            Assert.Equal(65m, line.UnitPrice);
            Assert.Equal(130m, line.LineTotal);
            Assert.Equal(130m, createdOrder.TotalAmount);
        }

        [Fact]
        public async Task BankTransfer_CreatesReservedOrderAndReturnsTypedInstructions()
        {
            var product = ConfigureProduct(price: 19.5m, quantity: 2);
            ConfigurePaymentMethod(PaymentMethodIds.BankTransfer, "localized bank label");
            Order? createdOrder = null;
            _inventory.Setup(service => service.CreateOrderWithInventoryAsync(
                    It.IsAny<Order>(),
                    InventoryReservationStatus.Reserved,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Order, InventoryReservationStatus, Guid, CancellationToken>((order, _, _, _) => createdOrder = order)
                .ReturnsAsync(new InventoryReservationResult(true));

            var result = await CheckoutAsync(
                PaymentMethodIds.BankTransfer,
                new CartLineRequest(product.Id, null, 2));

            Assert.True(result.Success);
            Assert.NotNull(createdOrder);
            Assert.NotNull(result.Payload);
            Assert.Equal(createdOrder!.Id, result.Payload!.OrderId);
            Assert.Equal(CheckoutStatus.PendingPayment, result.Payload.Status);
            Assert.Equal(CheckoutPaymentKind.BankTransfer, result.Payload.PaymentKind);
            Assert.NotNull(result.Payload.BankTransfer);
            Assert.Equal(createdOrder.Reference, result.Payload.BankTransfer!.Reference);
            Assert.Equal(39m, result.Payload.BankTransfer.Amount);
            Assert.Equal("BG00TEST", result.Payload.BankTransfer.Iban);
            _inventory.Verify(service => service.CreateOrderWithInventoryAsync(
                createdOrder,
                InventoryReservationStatus.Reserved,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task BankTransfer_EmailFailureDoesNotInvalidateCommittedOrder()
        {
            var product = ConfigureProduct(price: 10m, quantity: 1);
            ConfigurePaymentMethod(PaymentMethodIds.BankTransfer, "Bank Transfer");
            _email.Setup(service => service.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));

            var result = await CheckoutAsync(
                PaymentMethodIds.BankTransfer,
                new CartLineRequest(product.Id, null, 1));

            Assert.True(result.Success);
            Assert.NotNull(result.Payload?.BankTransfer);
            _inventory.Verify(service => service.TransitionOrderAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<InventoryReservationStatus>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Stripe_CreatesPendingOrderBeforeProviderAndPassesAuthoritativeLines()
        {
            var product = ConfigureProduct(price: 80m, quantity: 2, name: "Camera");
            ConfigurePaymentMethod(PaymentMethodIds.CreditCard, "Credit Card");
            Order? createdOrder = null;
            StripeCheckoutInitialization? providerInitialization = null;
            _inventory.Setup(service => service.CreateOrderWithInventoryAsync(
                    It.IsAny<Order>(),
                    InventoryReservationStatus.Reserved,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Order, InventoryReservationStatus, Guid, CancellationToken>((order, _, _, _) => createdOrder = order)
                .ReturnsAsync(new InventoryReservationResult(true));
            _payment.Setup(service => service.Pay(
                    It.IsAny<StripeCheckoutInitialization>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Callback<StripeCheckoutInitialization, string, CancellationToken>((initialization, _, _) =>
                {
                    Assert.NotNull(createdOrder);
                    Assert.Equal(createdOrder!.Id, initialization.OrderId);
                    Assert.Equal(createdOrder.Reference, initialization.OrderReference);
                    Assert.Equal(PaymentOrderStatus.PendingPayment, createdOrder.Status);
                    providerInitialization = initialization;
                })
                .ReturnsAsync(new PaymentInitializationResult(
                    true,
                    "https://checkout.stripe.test/session",
                    ProviderSessionId: "cs_test",
                    ProviderPaymentIntentId: "pi_test"));

            var result = await CheckoutAsync(
                PaymentMethodIds.CreditCard,
                new CartLineRequest(product.Id, null, 2));

            Assert.True(result.Success);
            Assert.NotNull(result.Payload);
            Assert.Equal(createdOrder!.Id, result.Payload!.OrderId);
            Assert.Equal(CheckoutPaymentKind.Stripe, result.Payload.PaymentKind);
            Assert.Equal(CheckoutStatus.PendingPayment, result.Payload.Status);
            Assert.Equal("https://checkout.stripe.test/session", result.Payload.RedirectUrl);
            var providerLine = Assert.Single(providerInitialization!.Lines);
            Assert.Equal(8000, providerLine.UnitAmount);
            Assert.Equal(
                (long)(createdOrder.Lines.Single().UnitPrice * 100),
                providerLine.UnitAmount);
        }

        [Fact]
        public async Task StripeInitializationFailure_ReleasesReservationAndMarksExistingOrderFailed()
        {
            var product = ConfigureProduct(price: 15m, quantity: 1);
            ConfigurePaymentMethod(PaymentMethodIds.CreditCard, "Credit Card");
            Order? createdOrder = null;
            _inventory.Setup(service => service.CreateOrderWithInventoryAsync(
                    It.IsAny<Order>(),
                    InventoryReservationStatus.Reserved,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Order, InventoryReservationStatus, Guid, CancellationToken>((order, _, _, _) => createdOrder = order)
                .ReturnsAsync(new InventoryReservationResult(true));
            _payment.Setup(service => service.Pay(
                    It.IsAny<StripeCheckoutInitialization>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Stripe unavailable",
                    FailureKind: PaymentInitializationFailureKind.Definitive));
            var result = await CheckoutAsync(
                PaymentMethodIds.CreditCard,
                new CartLineRequest(product.Id, null, 1));

            Assert.False(result.Success);
            Assert.Equal("Stripe unavailable", result.Message);
            Assert.NotNull(createdOrder);
            _stripeTransitions.Verify(service => service.TransitionAsync(
                It.IsAny<Guid>(),
                PaymentTransactionStatus.Failed,
                It.IsAny<CancellationToken>()), Times.Once);
            _inventory.Verify(service => service.TransitionOrderAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<InventoryReservationStatus>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StripeAmbiguousFailure_KeepsReservationForSameKeyRecovery()
        {
            var product = ConfigureProduct(price: 15m, quantity: 1);
            ConfigurePaymentMethod(PaymentMethodIds.CreditCard, "Credit Card");
            Order? createdOrder = null;
            _inventory.Setup(service => service.CreateOrderWithInventoryAsync(
                    It.IsAny<Order>(),
                    InventoryReservationStatus.Reserved,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Order, InventoryReservationStatus, Guid, CancellationToken>((order, _, _, _) => createdOrder = order)
                .ReturnsAsync(new InventoryReservationResult(true));
            _payment.Setup(service => service.Pay(
                    It.IsAny<StripeCheckoutInitialization>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Provider response uncertain",
                    FailureKind: PaymentInitializationFailureKind.Ambiguous));
            _inventory.Setup(service => service.TransitionOrderAsync(
                    It.IsAny<Guid>(),
                    PaymentOrderStatus.PaymentFailed,
                    InventoryReservationStatus.Released,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InventoryTransitionResult(InventoryTransitionOutcome.Applied));

            var result = await CheckoutAsync(
                PaymentMethodIds.CreditCard,
                new CartLineRequest(product.Id, null, 1));

            Assert.False(result.Success);
            Assert.Equal(CheckoutExecutionStatus.InProgress, result.Status);
            _inventory.Verify(service => service.TransitionOrderAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<InventoryReservationStatus>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StripeRetryDeadlineWithoutProviderIdentity_RemainsNonTerminal()
        {
            var product = ConfigureProduct(price: 15m, quantity: 1);
            ConfigurePaymentMethod(PaymentMethodIds.CreditCard, "Credit Card");
            _idempotency.Setup(store => store.PrepareProviderInitializationAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<StripeCheckoutInitialization>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid _, StripeCheckoutInitialization initialization, CancellationToken _) =>
                    new CheckoutProviderInitialization(DateTime.UtcNow.AddHours(-24), initialization));

            var result = await CheckoutAsync(
                PaymentMethodIds.CreditCard,
                new CartLineRequest(product.Id, null, 1));

            Assert.Equal(CheckoutExecutionStatus.InProgress, result.Status);
            Assert.Contains("requires reconciliation", result.Message, StringComparison.OrdinalIgnoreCase);
            VerifyStripeWasNotCalled();
            _payment.Verify(service => service.RecoverAsync(
                It.IsAny<StripePaymentRecoveryRequest>(),
                It.IsAny<CancellationToken>()), Times.Never);
            _stripeTransitions.Verify(service => service.TransitionAsync(
                It.IsAny<Guid>(),
                It.IsAny<PaymentTransactionStatus>(),
                It.IsAny<CancellationToken>()), Times.Never);
            _idempotency.Verify(store => store.SetPendingOutcomeAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.Is<PersistedCheckoutOutcome>(outcome => outcome.Status == CheckoutExecutionStatus.InProgress),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StripeRetryDeadlineWithConfirmedPaidProviderState_UsesAtomicPaidTransition()
        {
            var product = ConfigureProduct(price: 15m, quantity: 1);
            ConfigurePaymentMethod(PaymentMethodIds.CreditCard, "Credit Card");
            _paymentTransactions.Setup(store => store.GetOrCreateStripeAsync(
                    It.IsAny<Guid>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid orderId, long amount, string currency, CancellationToken _) =>
                    new PaymentTransaction
                    {
                        Id = Guid.NewGuid(),
                        OrderId = orderId,
                        Provider = PaymentProviderNames.Stripe,
                        ProviderSessionId = "cs_recovery",
                        ProviderPaymentIntentId = "pi_recovery",
                        ExpectedAmountMinor = amount,
                        Currency = currency,
                    });
            _idempotency.Setup(store => store.PrepareProviderInitializationAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<StripeCheckoutInitialization>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Guid _, Guid _, StripeCheckoutInitialization initialization, CancellationToken _) =>
                    new CheckoutProviderInitialization(DateTime.UtcNow.AddHours(-24), initialization));
            _payment.Setup(service => service.RecoverAsync(
                    It.IsAny<StripePaymentRecoveryRequest>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StripePaymentRecoveryResult(
                    StripePaymentRecoveryOutcome.Paid,
                    "pi_recovery"));

            var result = await CheckoutAsync(
                PaymentMethodIds.CreditCard,
                new CartLineRequest(product.Id, null, 1));

            Assert.True(result.Success);
            Assert.Equal(CheckoutStatus.Confirmed, result.Payload!.Status);
            VerifyStripeWasNotCalled();
            _stripeTransitions.Verify(service => service.TransitionAsync(
                It.IsAny<Guid>(),
                PaymentTransactionStatus.Paid,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ValidationFailure_CreatesNoOrderAndDoesNotCallStripe()
        {
            ConfigurePaymentMethod(PaymentMethodIds.CreditCard, "Credit Card");

            var result = await _orchestrator.CheckoutAsync(
                new Checkout { PaymentMethodId = PaymentMethodIds.CreditCard, Carts = [] },
                "customer-1",
                Guid.NewGuid());

            Assert.False(result.Success);
            VerifyInventoryWasNotCreated();
            VerifyStripeWasNotCalled();
        }

        [Fact]
        public async Task InventoryFailure_DoesNotCallStripe()
        {
            var product = ConfigureProduct(price: 10m, quantity: 1);
            ConfigurePaymentMethod(PaymentMethodIds.CreditCard, "Credit Card");
            _inventory.Setup(service => service.CreateOrderWithInventoryAsync(
                    It.IsAny<Order>(),
                    InventoryReservationStatus.Reserved,
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new InventoryReservationResult(false, "No stock"));

            var result = await CheckoutAsync(
                PaymentMethodIds.CreditCard,
                new CartLineRequest(product.Id, null, 1));

            Assert.False(result.Success);
            Assert.Equal("No stock", result.Message);
            VerifyStripeWasNotCalled();
        }

        [Fact]
        public async Task MissingAuthenticatedUser_CreatesNoOrder()
        {
            ConfigurePaymentMethod(PaymentMethodIds.CashOnDelivery, "Cash on Delivery");

            var result = await _orchestrator.CheckoutAsync(
                new Checkout { PaymentMethodId = PaymentMethodIds.CashOnDelivery, Carts = [] },
                string.Empty,
                Guid.NewGuid());

            Assert.False(result.Success);
            VerifyInventoryWasNotCreated();
        }

        [Fact]
        public async Task UnknownAuthenticatedCustomer_CreatesNoOrder()
        {
            _users.Setup(manager => manager.GetUserByIdAsync("deleted-customer"))
                .ReturnsAsync((AppUser?)null);
            ConfigurePaymentMethod(PaymentMethodIds.CashOnDelivery, "Cash on Delivery");

            var result = await _orchestrator.CheckoutAsync(
                new Checkout
                {
                    PaymentMethodId = PaymentMethodIds.CashOnDelivery,
                    Carts = [new CartLineRequest(Guid.NewGuid(), null, 1)],
                },
                "deleted-customer",
                Guid.NewGuid());

            Assert.False(result.Success);
            Assert.Contains("customer account", result.Message, StringComparison.OrdinalIgnoreCase);
            VerifyInventoryWasNotCreated();
        }

        [Fact]
        public async Task VariantFromAnotherProduct_IsRejectedBeforeOrderCreation()
        {
            var product = ConfigureProduct(price: 10m, quantity: 0);
            var variant = new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = Guid.NewGuid(),
                Price = 12m,
                Stock = 1,
            };
            _products.Setup(repository => repository.GetProductVariantsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, ProductVariant> { [variant.Id] = variant });
            ConfigurePaymentMethod(PaymentMethodIds.CashOnDelivery, "Cash on Delivery");

            var result = await CheckoutAsync(
                PaymentMethodIds.CashOnDelivery,
                new CartLineRequest(product.Id, variant.Id, 1));

            Assert.False(result.Success);
            Assert.Contains("does not belong", result.Message, StringComparison.OrdinalIgnoreCase);
            VerifyInventoryWasNotCreated();
        }

        [Fact]
        public async Task MissingVariantForVariantBackedProduct_IsRejectedBeforeOrderCreation()
        {
            var product = ConfigureProduct(price: 10m, quantity: 99);
            _products.Setup(repository => repository.GetProductIdsWithVariantsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new HashSet<Guid> { product.Id });
            ConfigurePaymentMethod(PaymentMethodIds.CashOnDelivery, "Cash on Delivery");

            var result = await CheckoutAsync(
                PaymentMethodIds.CashOnDelivery,
                new CartLineRequest(product.Id, null, 1));

            Assert.False(result.Success);
            Assert.Contains("variant must be selected", result.Message, StringComparison.OrdinalIgnoreCase);
            VerifyInventoryWasNotCreated();
        }

        [Fact]
        public async Task QuantityAboveCurrentAvailability_IsRejectedBeforeOrderCreation()
        {
            var product = ConfigureProduct(price: 10m, quantity: 1);
            ConfigurePaymentMethod(PaymentMethodIds.CashOnDelivery, "Cash on Delivery");

            var result = await CheckoutAsync(
                PaymentMethodIds.CashOnDelivery,
                new CartLineRequest(product.Id, null, 2));

            Assert.False(result.Success);
            Assert.Contains("exceeds", result.Message, StringComparison.OrdinalIgnoreCase);
            VerifyInventoryWasNotCreated();
        }

        private Product ConfigureProduct(decimal price, int quantity, string name = "Product")
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = "Description",
                Price = price,
                Quantity = quantity,
            };
            _products.Setup(repository => repository.GetProductsByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                .ReturnsAsync(new Dictionary<Guid, Product> { [product.Id] = product });
            return product;
        }

        private void ConfigurePaymentMethod(Guid id, string name)
        {
            _paymentMethods.Setup(service => service.GetPaymentMethodsAsync())
                .ReturnsAsync([new GetPaymentMethod { Id = id, Name = name }]);
        }

        private Task<CheckoutExecutionResult> CheckoutAsync(
            Guid paymentMethodId,
            params CartLineRequest[] lines)
        {
            return _orchestrator.CheckoutAsync(
                new Checkout { PaymentMethodId = paymentMethodId, Carts = lines },
                "customer-1",
                Guid.NewGuid());
        }

        private void VerifyInventoryWasNotCreated()
        {
            _inventory.Verify(service => service.CreateOrderWithInventoryAsync(
                It.IsAny<Order>(),
                It.IsAny<InventoryReservationStatus>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        private void VerifyStripeWasNotCalled()
        {
            _payment.Verify(service => service.Pay(
                It.IsAny<StripeCheckoutInitialization>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
