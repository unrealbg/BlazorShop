namespace BlazorShop.Tests.Infrastructure.PostgreSql
{
    using System.Collections.Concurrent;
    using System.Data.Common;
    using System.Text.Json;

    using BlazorShop.Application.DTOs;
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
    using BlazorShop.Infrastructure.Repositories;
    using BlazorShop.Infrastructure.Repositories.Payment;
    using BlazorShop.Infrastructure.Services;
    using BlazorShop.Tests.TestUtilities;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Moq;

    using Xunit;

    [Collection(PostgreSqlCollection.Name)]
    public sealed class CheckoutIdempotencyPostgreSqlTests
    {
        private readonly PostgreSqlFixture _database;

        public CheckoutIdempotencyPostgreSqlTests(PostgreSqlFixture database)
        {
            _database = database;
        }

        [Fact]
        public async Task SequentialCodReplay_ReturnsOriginalOrderAndConsumesStockOnce()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 3);
            var key = Guid.NewGuid();
            var checkout = CreateCheckout(PaymentMethodIds.CashOnDelivery, productId, quantity: 1);

            var first = await ExecuteAsync(checkout, "customer-1", key);
            var replay = await ExecuteAsync(checkout, "customer-1", key);

            Assert.True(first.Success);
            Assert.True(replay.Success);
            Assert.True(replay.IsReplay);
            Assert.Equal(first.Payload!.OrderId, replay.Payload!.OrderId);
            Assert.Equal(first.Payload.OrderReference, replay.Payload.OrderReference);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(1, await assertionContext.Orders.CountAsync());
            Assert.Equal(2, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Single(await assertionContext.InventoryReservations.ToListAsync());
            var record = await assertionContext.CheckoutIdempotencyRecords.SingleAsync();
            Assert.Equal(CheckoutIdempotencyState.Completed, record.State);
            Assert.NotNull(record.CompletedOn);
            Assert.True(record.ExpiresOn >= record.CompletedOn!.Value.AddDays(7));
        }

        [Fact]
        public async Task ConcurrentCodDuplicate_CreatesOneOrderAndOneInventoryMutation()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            var key = Guid.NewGuid();
            var checkout = CreateCheckout(PaymentMethodIds.CashOnDelivery, productId, quantity: 1);

            var results = await RunConcurrentlyAsync(
                () => ExecuteAsync(checkout, "customer-1", key),
                () => ExecuteAsync(checkout, "customer-1", key));

            Assert.All(results, result => Assert.True(result.Success));
            Assert.Equal(results[0].Payload!.OrderId, results[1].Payload!.OrderId);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(1, await assertionContext.Orders.CountAsync());
            Assert.Equal(1, await assertionContext.InventoryReservations.CountAsync());
            Assert.Equal(1, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(1, await assertionContext.CheckoutIdempotencyRecords.CountAsync());
        }

        [Fact]
        public async Task SequentialBankReplay_ReusesReservationInstructionsAndDoesNotResendEmail()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            var key = Guid.NewGuid();
            var checkout = CreateCheckout(PaymentMethodIds.BankTransfer, productId, quantity: 1);
            var email = new Mock<IEmailService>();

            var first = await ExecuteAsync(checkout, "customer-1", key, email: email.Object);
            var replay = await ExecuteAsync(checkout, "customer-1", key, email: email.Object);

            Assert.Equal(first.Payload!.OrderId, replay.Payload!.OrderId);
            Assert.Equal(
                first.Payload.BankTransfer!.Reference,
                replay.Payload.BankTransfer!.Reference);
            email.Verify(service => service.SendEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()), Times.Once);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(1, await assertionContext.Orders.CountAsync());
            Assert.Equal(
                InventoryReservationStatus.Reserved,
                (await assertionContext.InventoryReservations.SingleAsync()).Status);
        }

        [Fact]
        public async Task SequentialAndConcurrentStripeReplay_UseOneLogicalProviderSession()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            var key = Guid.NewGuid();
            var checkout = CreateCheckout(PaymentMethodIds.CreditCard, productId, quantity: 1);
            var provider = new RecordingPaymentService(async call =>
            {
                await Task.Delay(150);
                return new PaymentInitializationResult(
                    true,
                    $"https://checkout.stripe.test/{call.ProviderIdempotencyKey}");
            });

            var concurrent = await RunConcurrentlyAsync(
                () => ExecuteAsync(checkout, "customer-1", key, provider),
                () => ExecuteAsync(checkout, "customer-1", key, provider));
            var sequentialReplay = await ExecuteAsync(checkout, "customer-1", key, provider);

            Assert.All(concurrent, result => Assert.True(result.Success));
            Assert.True(sequentialReplay.Success);
            Assert.Equal(concurrent[0].Payload!.OrderId, concurrent[1].Payload!.OrderId);
            Assert.Equal(concurrent[0].Payload!.RedirectUrl, sequentialReplay.Payload!.RedirectUrl);
            var providerCall = Assert.Single(provider.Calls);
            Assert.StartsWith("blazorshop-checkout-", providerCall.ProviderIdempotencyKey, StringComparison.Ordinal);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(1, await assertionContext.Orders.CountAsync());
            Assert.Equal(1, await assertionContext.InventoryReservations.CountAsync());
            Assert.Equal(1, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            var paymentTransaction = await assertionContext.PaymentTransactions.SingleAsync();
            var identityReplay = await new PaymentTransactionStore(_database.CreateContextFactory())
                .PersistProviderIdentityAsync(
                    paymentTransaction.Id,
                    paymentTransaction.ProviderSessionId!,
                    providerPaymentIntentId: null);
            Assert.Equal(PaymentProviderIdentityPersistenceOutcome.AlreadyPersisted, identityReplay.Outcome);
            Assert.Equal(PaymentTransactionStatus.Pending, identityReplay.PaymentStatus);
        }

        [Fact]
        public async Task SameKeyWithDifferentIntent_ReturnsConflictWithoutNewSideEffects()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 5);
            var otherProductId = await SeedProductAsync(quantity: 5);
            var key = Guid.NewGuid();
            var original = CreateCheckout(PaymentMethodIds.CashOnDelivery, productId, quantity: 1);
            Assert.True((await ExecuteAsync(original, "customer-1", key)).Success);

            var quantityConflict = await ExecuteAsync(
                CreateCheckout(PaymentMethodIds.CashOnDelivery, productId, quantity: 2),
                "customer-1",
                key);
            var productConflict = await ExecuteAsync(
                CreateCheckout(PaymentMethodIds.CashOnDelivery, otherProductId, quantity: 1),
                "customer-1",
                key);
            var paymentConflict = await ExecuteAsync(
                CreateCheckout(PaymentMethodIds.BankTransfer, productId, quantity: 1),
                "customer-1",
                key);
            var variantConflict = await ExecuteAsync(
                new Checkout
                {
                    PaymentMethodId = PaymentMethodIds.CashOnDelivery,
                    Carts = [new CartLineRequest(productId, Guid.NewGuid(), 1)],
                },
                "customer-1",
                key);

            Assert.Equal(CheckoutExecutionStatus.Conflict, quantityConflict.Status);
            Assert.Equal(CheckoutExecutionStatus.Conflict, productConflict.Status);
            Assert.Equal(CheckoutExecutionStatus.Conflict, paymentConflict.Status);
            Assert.Equal(CheckoutExecutionStatus.Conflict, variantConflict.Status);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(1, await assertionContext.Orders.CountAsync());
            Assert.Equal(4, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(5, (await assertionContext.Products.FindAsync(otherProductId))!.Quantity);
        }

        [Fact]
        public async Task SameKeyForDifferentUsers_IsIndependentlyScoped()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 3);
            var key = Guid.NewGuid();
            var checkout = CreateCheckout(PaymentMethodIds.CashOnDelivery, productId, quantity: 1);

            var first = await ExecuteAsync(checkout, "customer-a", key);
            var second = await ExecuteAsync(checkout, "customer-b", key);

            Assert.True(first.Success);
            Assert.True(second.Success);
            Assert.NotEqual(first.Payload!.OrderId, second.Payload!.OrderId);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(2, await assertionContext.Orders.CountAsync());
            Assert.Equal(2, await assertionContext.CheckoutIdempotencyRecords.CountAsync());
            Assert.Equal(1, (await assertionContext.Products.FindAsync(productId))!.Quantity);
        }

        [Fact]
        public async Task CommittedOrderWithAbandonedProcessingLease_IsRecoveredWithoutSecondDeduction()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            var key = Guid.NewGuid();
            var checkout = CreateCheckout(PaymentMethodIds.CashOnDelivery, productId, quantity: 1);
            var canonical = CheckoutIntentCanonicalizer.Canonicalize(checkout);
            var store = CreateStore();
            var claim = await store.ClaimAsync(
                "customer-1",
                key,
                canonical.Fingerprint!,
                checkout.PaymentMethodId);
            var order = new Order
            {
                Id = claim.Record.OrderId,
                UserId = "customer-1",
                OrderStatus = OrderStatus.Confirmed,
                PaymentStatus = OrderPaymentStatus.Pending,
                PaymentMethod = OrderPaymentMethod.CashOnDelivery,
                Reference = claim.Record.OrderReference,
                TotalAmount = 10m,
                SubtotalAmount = 10m,
                Currency = "EUR",
                Lines =
                [
                    new OrderLine
                    {
                        ProductId = productId,
                        ProductNameSnapshot = "Product",
                        Quantity = 1,
                        UnitPrice = 10m,
                        LineTotal = 10m,
                    },
                ],
            };
            var inventory = new InventoryReservationService(_database.CreateContextFactory());
            Assert.True((await inventory.CreateOrderWithInventoryAsync(
                order,
                InventoryReservationStatus.Consumed,
                claim.Record.Id)).Success);
            await using (var leaseContext = _database.CreateContext())
            {
                await leaseContext.CheckoutIdempotencyRecords
                    .Where(record => record.Id == claim.Record.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(record => record.LeaseExpiresOn, DateTime.UtcNow.AddSeconds(-1)));
            }

            var recovered = await ExecuteAsync(checkout, "customer-1", key);

            Assert.True(recovered.Success);
            Assert.Equal(order.Id, recovered.Payload!.OrderId);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(1, await assertionContext.Orders.CountAsync());
            Assert.Equal(1, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(1, await assertionContext.InventoryReservations.CountAsync());
        }

        [Fact]
        public async Task StripeAmbiguousResponse_RecoversWithSameProviderKeyAndOneLogicalSession()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            var key = Guid.NewGuid();
            var checkout = CreateCheckout(PaymentMethodIds.CreditCard, productId, quantity: 1);
            var sessions = new ConcurrentDictionary<string, string>();
            var providerRequests = new ConcurrentDictionary<string, string>();
            var attempts = 0;
            var provider = new RecordingPaymentService(call =>
            {
                var request = JsonSerializer.Serialize(call.Initialization);
                if (providerRequests.TryGetValue(call.ProviderIdempotencyKey, out var existingRequest)
                    && !string.Equals(existingRequest, request, StringComparison.Ordinal))
                {
                    return Task.FromResult(new PaymentInitializationResult(
                        false,
                        ErrorMessage: "Idempotency parameters did not match",
                        FailureKind: PaymentInitializationFailureKind.Definitive));
                }

                providerRequests.TryAdd(call.ProviderIdempotencyKey, request);
                var url = sessions.GetOrAdd(
                    call.ProviderIdempotencyKey,
                    value => $"https://checkout.stripe.test/{value}");
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    return Task.FromResult(new PaymentInitializationResult(
                        false,
                        ErrorMessage: "Response lost",
                        FailureKind: PaymentInitializationFailureKind.Ambiguous));
                }

                return Task.FromResult(new PaymentInitializationResult(true, url));
            });

            var ambiguous = await ExecuteAsync(checkout, "customer-1", key, provider);
            var recovered = await ExecuteAsync(checkout, "customer-1", key, provider);

            Assert.Equal(CheckoutExecutionStatus.InProgress, ambiguous.Status);
            Assert.True(recovered.Success);
            Assert.Single(sessions);
            Assert.Equal(2, provider.Calls.Count);
            Assert.Single(provider.Calls.Select(call => call.ProviderIdempotencyKey).Distinct());
            Assert.Single(provider.Calls.Select(call => JsonSerializer.Serialize(call.Initialization)).Distinct());
            Assert.Null(Assert.Single(provider.Calls.First().Initialization.Lines).Description);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(1, await assertionContext.Orders.CountAsync());
            Assert.Equal(1, await assertionContext.InventoryReservations.CountAsync());
            Assert.Equal(1, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            var record = await assertionContext.CheckoutIdempotencyRecords.SingleAsync();
            Assert.NotNull(record.ProviderInitializationStartedOn);
            Assert.NotNull(record.ProviderInitializationJson);
            Assert.Single(await assertionContext.PaymentTransactions.ToListAsync());
        }

        [Fact]
        public async Task ProviderIdentityPersistenceFailure_RetryUsesSameProviderRequestAndOneTransaction()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            var checkout = CreateCheckout(PaymentMethodIds.CreditCard, productId, quantity: 1);
            var provider = new RecordingPaymentService(_ => Task.FromResult(
                new PaymentInitializationResult(true, "https://checkout.stripe.test/recovered")));
            var transactionStore = new FailFirstIdentityPersistenceStore(
                new PaymentTransactionStore(_database.CreateContextFactory()));
            var key = Guid.NewGuid();

            var ambiguous = await ExecuteAsync(
                checkout,
                "customer-1",
                key,
                provider,
                paymentTransactionStore: transactionStore);
            var recovered = await ExecuteAsync(
                checkout,
                "customer-1",
                key,
                provider,
                paymentTransactionStore: transactionStore);

            Assert.Equal(CheckoutExecutionStatus.InProgress, ambiguous.Status);
            Assert.True(recovered.Success);
            var calls = provider.Calls.ToArray();
            Assert.Equal(2, calls.Length);
            Assert.Single(calls.Select(call => call.ProviderIdempotencyKey).Distinct());
            Assert.Single(calls.Select(call => JsonSerializer.Serialize(call.Initialization)).Distinct());
            await using var assertionContext = _database.CreateContext();
            var paymentTransaction = await assertionContext.PaymentTransactions.SingleAsync();
            Assert.Equal($"cs_{calls[0].ProviderIdempotencyKey}", paymentTransaction.ProviderSessionId);
            Assert.Equal($"pi_{calls[0].ProviderIdempotencyKey}", paymentTransaction.ProviderPaymentIntentId);
            Assert.Equal(1, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(InventoryReservationStatus.Reserved,
                (await assertionContext.InventoryReservations.SingleAsync()).Status);
        }

        [Fact]
        public async Task StripeCheckout_NormalizesRoundingOnceAcrossOrderTransactionAndProvider()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2, price: 1.005m);
            var checkout = CreateCheckout(PaymentMethodIds.CreditCard, productId, quantity: 1);
            var provider = new RecordingPaymentService(_ => Task.FromResult(
                new PaymentInitializationResult(true, "https://checkout.stripe.test/rounded")));

            var result = await ExecuteAsync(checkout, "customer-1", Guid.NewGuid(), provider);

            Assert.True(result.Success);
            var providerLine = Assert.Single(Assert.Single(provider.Calls).Initialization.Lines);
            Assert.Equal(101, providerLine.UnitAmount);
            await using var assertionContext = _database.CreateContext();
            var order = await assertionContext.Orders.Include(item => item.Lines).SingleAsync();
            Assert.Equal(1.01m, order.TotalAmount);
            Assert.Equal("EUR", order.Currency);
            Assert.Equal(1.01m, Assert.Single(order.Lines).UnitPrice);
            Assert.Equal(1.01m, Assert.Single(order.Lines).LineTotal);
            var paymentTransaction = await assertionContext.PaymentTransactions.SingleAsync();
            Assert.Equal(101, paymentTransaction.ExpectedAmountMinor);
            Assert.Equal("EUR", paymentTransaction.Currency);
        }

        [Fact]
        public async Task StripeMultiLineRecovery_UsesStableSnapshotAndDeterministicOrdering()
        {
            await _database.ResetDatabaseAsync();
            var firstProductId = await SeedProductAsync(quantity: 2);
            var secondProductId = await SeedProductAsync(quantity: 2);
            var orderedIds = new[] { firstProductId, secondProductId }.Order().ToArray();
            var checkout = new Checkout
            {
                PaymentMethodId = PaymentMethodIds.CreditCard,
                Carts =
                [
                    new CartLineRequest(orderedIds[1], null, 1),
                    new CartLineRequest(orderedIds[0], null, 1),
                ],
            };
            var attempts = 0;
            var provider = new RecordingPaymentService(_ => Task.FromResult(
                Interlocked.Increment(ref attempts) == 1
                    ? new PaymentInitializationResult(
                        false,
                        ErrorMessage: "Response lost",
                        FailureKind: PaymentInitializationFailureKind.Ambiguous)
                    : new PaymentInitializationResult(true, "https://checkout.stripe.test/stable")));
            var key = Guid.NewGuid();

            Assert.Equal(
                CheckoutExecutionStatus.InProgress,
                (await ExecuteAsync(checkout, "customer-1", key, provider)).Status);
            Assert.True((await ExecuteAsync(checkout, "customer-1", key, provider)).Success);

            var calls = provider.Calls.ToArray();
            Assert.Equal(2, calls.Length);
            Assert.Equal(
                JsonSerializer.Serialize(calls[0].Initialization),
                JsonSerializer.Serialize(calls[1].Initialization));
            Assert.Equal(
                orderedIds,
                calls[0].Initialization.Lines.Select(line => line.ProductId).ToArray());
        }

        [Fact]
        public async Task StripeVariantRecovery_UsesIdenticalPersistedVariantParameters()
        {
            await _database.ResetDatabaseAsync();
            var (productId, variantId) = await SeedVariantProductAsync(stock: 2);
            var checkout = new Checkout
            {
                PaymentMethodId = PaymentMethodIds.CreditCard,
                Carts = [new CartLineRequest(productId, variantId, 1)],
            };
            var attempts = 0;
            var provider = new RecordingPaymentService(_ => Task.FromResult(
                Interlocked.Increment(ref attempts) == 1
                    ? new PaymentInitializationResult(
                        false,
                        ErrorMessage: "Response lost",
                        FailureKind: PaymentInitializationFailureKind.Ambiguous)
                    : new PaymentInitializationResult(true, "https://checkout.stripe.test/variant")));
            var key = Guid.NewGuid();

            Assert.Equal(
                CheckoutExecutionStatus.InProgress,
                (await ExecuteAsync(checkout, "customer-1", key, provider)).Status);
            Assert.True((await ExecuteAsync(checkout, "customer-1", key, provider)).Success);

            var calls = provider.Calls.ToArray();
            Assert.Equal(2, calls.Length);
            Assert.Equal(
                JsonSerializer.Serialize(calls[0].Initialization),
                JsonSerializer.Serialize(calls[1].Initialization));
            var line = Assert.Single(calls[0].Initialization.Lines);
            Assert.Equal(variantId, line.ProductVariantId);
            Assert.Equal("SKU: TEST-VARIANT, Size: ShoesUS 10, Color: Black", line.Description);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(1, (await assertionContext.ProductVariants.FindAsync(variantId))!.Stock);
        }

        [Theory]
        [InlineData("Stripe authentication failed (401).")]
        [InlineData("Stripe permission denied (403).")]
        public async Task DefinitiveRetryFailureAfterAmbiguousDispatch_RemainsNonTerminal(
            string retryFailure)
        {
            var attempt = 0;
            var provider = new RecordingPaymentService(_ => Task.FromResult(
                Interlocked.Increment(ref attempt) == 1
                    ? new PaymentInitializationResult(
                        false,
                        ErrorMessage: "The initial provider response was lost.",
                        FailureKind: PaymentInitializationFailureKind.Ambiguous)
                    : new PaymentInitializationResult(
                        false,
                        ErrorMessage: retryFailure,
                        FailureKind: PaymentInitializationFailureKind.Definitive)));
            var seed = await SeedAmbiguousStripeCheckoutAsync(provider);

            var retry = await ExecuteAsync(seed.Checkout, "customer-1", seed.Key, provider);

            Assert.Equal(CheckoutExecutionStatus.InProgress, retry.Status);
            Assert.Contains("requires reconciliation", retry.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(2, provider.Calls.Count);
            Assert.Empty(provider.RecoveryCalls);
            await AssertUnresolvedSeedStateAsync(seed);
        }

        [Fact]
        public async Task LeaseTakeoverReturnsDispatchMarkerPersistedAfterPreAcquisitionRead()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            var checkout = CreateCheckout(PaymentMethodIds.CreditCard, productId, quantity: 1);
            var key = Guid.NewGuid();
            var preparationBarrier = new ProviderPreparationBarrierStore(CreateStore());
            var firstProvider = new RecordingPaymentService(_ => Task.FromResult(
                new PaymentInitializationResult(
                    false,
                    ErrorMessage: "The provider response was lost.",
                    FailureKind: PaymentInitializationFailureKind.Ambiguous)));
            var firstAttempt = ExecuteAsync(
                checkout,
                "customer-1",
                key,
                firstProvider,
                idempotencyStore: preparationBarrier);
            await preparationBarrier.PreparationReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await ExpireCheckoutLeaseAsync();

            var acquisitionBarrier = new PreAcquisitionReadBarrierInterceptor();
            var secondStore = CreateStore(acquisitionBarrier);
            var secondProvider = new RecordingPaymentService(_ => Task.FromResult(
                new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Stripe authentication failed.",
                    FailureKind: PaymentInitializationFailureKind.Definitive)));
            var secondAttempt = ExecuteAsync(
                checkout,
                "customer-1",
                key,
                secondProvider,
                idempotencyStore: secondStore);
            await acquisitionBarrier.ReadCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            preparationBarrier.ResumePreparation.TrySetResult();
            Assert.Equal(CheckoutExecutionStatus.InProgress, (await firstAttempt).Status);
            acquisitionBarrier.ResumeAcquisition.TrySetResult();
            var retry = await secondAttempt;

            Assert.Equal(CheckoutExecutionStatus.InProgress, retry.Status);
            Assert.Single(firstProvider.Calls);
            Assert.Single(secondProvider.Calls);
            await using var context = _database.CreateContext();
            Assert.Equal(PaymentTransactionStatus.Pending,
                (await context.PaymentTransactions.SingleAsync()).Status);
            Assert.Equal(OrderPaymentStatus.Pending, (await context.Orders.SingleAsync()).PaymentStatus);
            Assert.Equal(InventoryReservationStatus.Reserved,
                (await context.InventoryReservations.SingleAsync()).Status);
            Assert.Equal(1, (await context.Products.FindAsync(productId))!.Quantity);
            var record = await context.CheckoutIdempotencyRecords.SingleAsync();
            Assert.NotNull(record.ProviderInitializationStartedOn);
            Assert.Contains("requires reconciliation", record.OutcomeJson!, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task StaleDefinitiveFailureCannotReleaseAfterNewOwnerCreatesPayableSession()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            var checkout = CreateCheckout(PaymentMethodIds.CreditCard, productId, quantity: 1);
            var key = Guid.NewGuid();
            var pausingTransition = new PauseBeforeDefinitiveFailureTransitionService(
                CreateStripeTransitionService());
            var rejectedProvider = new RecordingPaymentService(_ => Task.FromResult(
                new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Initial request was rejected.",
                    FailureKind: PaymentInitializationFailureKind.Definitive)));
            var staleAttempt = ExecuteAsync(
                checkout,
                "customer-1",
                key,
                rejectedProvider,
                stripeTransitionService: pausingTransition);
            await pausingTransition.TransitionReached.Task.WaitAsync(TimeSpan.FromSeconds(10));

            string fingerprint;
            await using (var context = _database.CreateContext())
            {
                var record = await context.CheckoutIdempotencyRecords.SingleAsync();
                fingerprint = record.RequestFingerprint;
                record.LeaseExpiresOn = DateTime.UtcNow.AddSeconds(-1);
                await context.SaveChangesAsync();
            }

            var takeoverStore = CreateStore();
            var takeover = await takeoverStore.ClaimAsync(
                "customer-1",
                key,
                fingerprint,
                PaymentMethodIds.CreditCard);
            Assert.Equal(CheckoutIdempotencyClaimStatus.Acquired, takeover.Status);
            Assert.NotNull(takeover.Outcome?.DefinitiveInitialRejectionId);
            var reconciliationMarker = new PersistedCheckoutOutcome(
                1,
                CheckoutExecutionStatus.InProgress,
                new ServiceResponse<CheckoutResult>(false, "Requires reconciliation."));
            Assert.True(await takeoverStore.SetPendingOutcomeAsync(
                takeover.Record.Id,
                takeover.LeaseOwnerId,
                reconciliationMarker));
            Assert.True(await takeoverStore.ReleaseLeaseAsync(
                takeover.Record.Id,
                takeover.LeaseOwnerId));

            var payableProvider = new RecordingPaymentService(_ => Task.FromResult(
                new PaymentInitializationResult(
                    true,
                    "https://checkout.stripe.test/payable",
                    ProviderSessionId: "cs_payable",
                    ProviderPaymentIntentId: "pi_payable")));
            var payable = await ExecuteAsync(checkout, "customer-1", key, payableProvider);
            Assert.True(payable.Success);
            Assert.Equal("https://checkout.stripe.test/payable", payable.Payload!.RedirectUrl);

            pausingTransition.ResumeTransition.TrySetResult();
            Assert.Equal(CheckoutExecutionStatus.InProgress, (await staleAttempt).Status);

            await using var assertionContext = _database.CreateContext();
            var transaction = await assertionContext.PaymentTransactions.SingleAsync();
            Assert.Equal(PaymentTransactionStatus.Pending, transaction.Status);
            Assert.Equal("cs_payable", transaction.ProviderSessionId);
            Assert.Equal(OrderPaymentStatus.Pending,
                (await assertionContext.Orders.SingleAsync()).PaymentStatus);
            Assert.Equal(InventoryReservationStatus.Reserved,
                (await assertionContext.InventoryReservations.SingleAsync()).Status);
            Assert.Equal(1, (await assertionContext.Products.FindAsync(productId))!.Quantity);
        }

        [Fact]
        public async Task ProvenDefinitiveInitialRejectionRecoversAfterCrashAndReleasesExactlyOnce()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            var checkout = CreateCheckout(PaymentMethodIds.CreditCard, productId, quantity: 1);
            var key = Guid.NewGuid();
            var rejectedProvider = new RecordingPaymentService(_ => Task.FromResult(
                new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Initial request was rejected.",
                    FailureKind: PaymentInitializationFailureKind.Definitive)));

            await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteAsync(
                checkout,
                "customer-1",
                key,
                rejectedProvider,
                stripeTransitionService: new CrashBeforeDefinitiveFailureTransitionService()));
            await ExpireCheckoutLeaseAsync();

            var providerMustNotReopen = new RecordingPaymentService(_ => Task.FromResult(
                new PaymentInitializationResult(true, "https://checkout.stripe.test/unsafe")));
            var recovered = await ExecuteAsync(checkout, "customer-1", key, providerMustNotReopen);
            var replay = await ExecuteAsync(checkout, "customer-1", key, providerMustNotReopen);

            Assert.False(recovered.Success);
            Assert.False(replay.Success);
            Assert.True(replay.IsReplay);
            Assert.Empty(providerMustNotReopen.Calls);
            await using var context = _database.CreateContext();
            Assert.Equal(PaymentTransactionStatus.Failed,
                (await context.PaymentTransactions.SingleAsync()).Status);
            Assert.Equal(OrderPaymentStatus.Failed, (await context.Orders.SingleAsync()).PaymentStatus);
            Assert.Equal(InventoryReservationStatus.Released,
                (await context.InventoryReservations.SingleAsync()).Status);
            Assert.Equal(2, (await context.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(CheckoutIdempotencyState.Failed,
                (await context.CheckoutIdempotencyRecords.SingleAsync()).State);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InvalidPersistedProviderLineSnapshotAfterPossibleDispatch_RemainsNonTerminal(
            bool useMismatchedLineCurrency)
        {
            var seed = await SeedAmbiguousStripeCheckoutAsync();
            await using (var context = _database.CreateContext())
            {
                var record = await context.CheckoutIdempotencyRecords.SingleAsync();
                var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
                var initialization = JsonSerializer.Deserialize<StripeCheckoutInitialization>(
                    record.ProviderInitializationJson!,
                    serializerOptions);
                Assert.NotNull(initialization);
                var line = Assert.Single(initialization.Lines);
                var invalidLine = useMismatchedLineCurrency
                    ? line with { Currency = "USD" }
                    : line with { UnitAmount = line.UnitAmount + 1 };
                record.ProviderInitializationJson = JsonSerializer.Serialize(
                    initialization with { Lines = [invalidLine] },
                    serializerOptions);
                record.LeaseExpiresOn = DateTime.UtcNow.AddSeconds(-1);
                await context.SaveChangesAsync();
            }

            var retry = await ExecuteAsync(seed.Checkout, "customer-1", seed.Key, seed.Provider);

            Assert.Equal(CheckoutExecutionStatus.InProgress, retry.Status);
            Assert.Single(seed.Provider.Calls);
            Assert.Empty(seed.Provider.RecoveryCalls);
            await AssertUnresolvedSeedStateAsync(seed);
        }

        [Fact]
        public async Task PaidProviderAttemptWithAuthenticationFailureDuringRecovery_CanStillBePaidByWebhook()
        {
            var attempt = 0;
            var provider = new RecordingPaymentService(
                _ => Task.FromResult(
                    Interlocked.Increment(ref attempt) == 1
                        ? new PaymentInitializationResult(
                            false,
                            ErrorMessage: "The successful provider response was lost.",
                            FailureKind: PaymentInitializationFailureKind.Ambiguous)
                        : new PaymentInitializationResult(
                            false,
                            ErrorMessage: "Stripe authentication failed (401).",
                            FailureKind: PaymentInitializationFailureKind.Definitive)),
                _ => throw new UnauthorizedAccessException("Provider recovery authentication failed."));
            var seed = await SeedAmbiguousStripeCheckoutAsync(provider);
            await using (var context = _database.CreateContext())
            {
                await context.PaymentTransactions
                    .Where(transaction => transaction.Id == seed.PaymentTransactionId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(transaction => transaction.ProviderSessionId, "cs_delayed_paid")
                        .SetProperty(transaction => transaction.ProviderPaymentIntentId, "pi_delayed_paid"));
            }

            var retry = await ExecuteAsync(seed.Checkout, "customer-1", seed.Key, provider);

            Assert.Equal(CheckoutExecutionStatus.InProgress, retry.Status);
            Assert.Equal(2, provider.Calls.Count);
            Assert.Single(provider.RecoveryCalls);
            await AssertUnresolvedSeedStateAsync(seed);

            var reconciliation = await CreateStripeReconciliationService().ReconcileAsync(
                CreateStripeEvent(seed, "evt_delayed_paid_after_recovery_auth") with
                {
                    SessionId = "cs_delayed_paid",
                    PaymentIntentId = "pi_delayed_paid",
                });

            Assert.Equal(StripePaymentReconciliationOutcome.Processed, reconciliation);
            await AssertAmbiguousSeedStateAsync(
                seed,
                PaymentTransactionStatus.Paid,
                OrderPaymentStatus.Paid,
                InventoryReservationStatus.Consumed,
                expectedStock: 1,
                CheckoutIdempotencyState.LocalCommitted);
        }

        [Fact]
        public async Task StripeRecoveryAfterProviderDeadline_WithUnknownProviderStateKeepsReservationAndKey()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            var checkout = CreateCheckout(PaymentMethodIds.CreditCard, productId, quantity: 1);
            var provider = new RecordingPaymentService(_ => Task.FromResult(new PaymentInitializationResult(
                false,
                ErrorMessage: "Response lost",
                FailureKind: PaymentInitializationFailureKind.Ambiguous)));
            var key = Guid.NewGuid();

            var ambiguous = await ExecuteAsync(checkout, "customer-1", key, provider);
            Assert.Equal(CheckoutExecutionStatus.InProgress, ambiguous.Status);
            await using (var expiryContext = _database.CreateContext())
            {
                await expiryContext.CheckoutIdempotencyRecords.ExecuteUpdateAsync(setters => setters
                    .SetProperty(
                        record => record.ProviderInitializationStartedOn,
                        DateTime.UtcNow.AddHours(-24))
                    .SetProperty(record => record.LeaseExpiresOn, DateTime.UtcNow.AddSeconds(-1)));
            }

            var unresolved = await ExecuteAsync(checkout, "customer-1", key, provider);
            var retry = await ExecuteAsync(checkout, "customer-1", key, provider);

            Assert.Equal(CheckoutExecutionStatus.InProgress, unresolved.Status);
            Assert.Contains("requires reconciliation", unresolved.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(CheckoutExecutionStatus.InProgress, retry.Status);
            Assert.Single(provider.Calls);
            Assert.Empty(provider.RecoveryCalls);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(1, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(OrderPaymentStatus.Pending, (await assertionContext.Orders.SingleAsync()).PaymentStatus);
            Assert.Equal(
                PaymentTransactionStatus.Pending,
                (await assertionContext.PaymentTransactions.SingleAsync()).Status);
            Assert.Equal(
                InventoryReservationStatus.Reserved,
                (await assertionContext.InventoryReservations.SingleAsync()).Status);
            var record = await assertionContext.CheckoutIdempotencyRecords.SingleAsync();
            Assert.Equal(CheckoutIdempotencyState.LocalCommitted, record.State);
            Assert.NotNull(record.OutcomeJson);
            Assert.Contains("requires reconciliation", record.OutcomeJson, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(key, record.IdempotencyKey);
            Assert.NotNull(record.ProviderInitializationStartedOn);
            Assert.NotNull(record.ProviderInitializationJson);
        }

        [Fact]
        public async Task ProviderLookupAfterDeadline_WhenPaid_CompletesOrderWithoutReleasingInventory()
        {
            var provider = new RecordingPaymentService(
                _ => Task.FromResult(new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Response lost",
                    FailureKind: PaymentInitializationFailureKind.Ambiguous)),
                _ => Task.FromResult(new StripePaymentRecoveryResult(
                    StripePaymentRecoveryOutcome.Paid,
                    "pi_recovery")));
            var seed = await SeedAmbiguousStripeCheckoutAsync(provider);
            await ExpireProviderRecoveryDeadlineAsync(seed);

            var recovered = await ExecuteAsync(seed.Checkout, "customer-1", seed.Key, provider);

            Assert.True(recovered.Success);
            Assert.Equal(CheckoutStatus.Confirmed, recovered.Payload!.Status);
            Assert.Single(provider.Calls);
            Assert.Single(provider.RecoveryCalls);
            await AssertAmbiguousSeedStateAsync(
                seed,
                PaymentTransactionStatus.Paid,
                OrderPaymentStatus.Paid,
                InventoryReservationStatus.Consumed,
                expectedStock: 1,
                CheckoutIdempotencyState.Completed);
        }

        [Fact]
        public async Task ProviderLookupAfterDeadline_WhenSessionRemainsOpen_KeepsInventoryReserved()
        {
            var provider = new RecordingPaymentService(
                _ => Task.FromResult(new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Response lost",
                    FailureKind: PaymentInitializationFailureKind.Ambiguous)),
                _ => Task.FromResult(new StripePaymentRecoveryResult(
                    StripePaymentRecoveryOutcome.Open,
                    "pi_recovery",
                    "The provider session remains open.")));
            var seed = await SeedAmbiguousStripeCheckoutAsync(provider);
            await ExpireProviderRecoveryDeadlineAsync(seed);

            var unresolved = await ExecuteAsync(seed.Checkout, "customer-1", seed.Key, provider);

            Assert.Equal(CheckoutExecutionStatus.InProgress, unresolved.Status);
            Assert.Single(provider.Calls);
            Assert.Single(provider.RecoveryCalls);
            await AssertUnresolvedSeedStateAsync(seed);
        }

        [Fact]
        public async Task ProviderLookupAfterDeadline_WhenLookupTimesOut_KeepsInventoryReserved()
        {
            var provider = new RecordingPaymentService(
                _ => Task.FromResult(new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Response lost",
                    FailureKind: PaymentInitializationFailureKind.Ambiguous)),
                _ => throw new TimeoutException("Provider lookup timed out."));
            var seed = await SeedAmbiguousStripeCheckoutAsync(provider);
            await ExpireProviderRecoveryDeadlineAsync(seed);

            var unresolved = await ExecuteAsync(seed.Checkout, "customer-1", seed.Key, provider);

            Assert.Equal(CheckoutExecutionStatus.InProgress, unresolved.Status);
            Assert.Single(provider.Calls);
            Assert.Single(provider.RecoveryCalls);
            await AssertUnresolvedSeedStateAsync(seed);
        }

        [Fact]
        public async Task ProviderLookupAfterDeadline_WhenExpirationConfirmed_ReleasesExactlyOnce()
        {
            var provider = new RecordingPaymentService(
                _ => Task.FromResult(new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Response lost",
                    FailureKind: PaymentInitializationFailureKind.Ambiguous)),
                _ => Task.FromResult(new StripePaymentRecoveryResult(
                    StripePaymentRecoveryOutcome.Expired,
                    "pi_recovery")));
            var seed = await SeedAmbiguousStripeCheckoutAsync(provider);
            await ExpireProviderRecoveryDeadlineAsync(seed);

            var expired = await ExecuteAsync(seed.Checkout, "customer-1", seed.Key, provider);
            var replay = await ExecuteAsync(seed.Checkout, "customer-1", seed.Key, provider);

            Assert.False(expired.Success);
            Assert.False(replay.Success);
            Assert.True(replay.IsReplay);
            Assert.Single(provider.Calls);
            Assert.Single(provider.RecoveryCalls);
            await AssertAmbiguousSeedStateAsync(
                seed,
                PaymentTransactionStatus.Cancelled,
                OrderPaymentStatus.Cancelled,
                InventoryReservationStatus.Released,
                expectedStock: 2,
                CheckoutIdempotencyState.Failed);
        }

        [Fact]
        public async Task PaymentCompletionDuringProviderExpiration_WinsOverStaleExpiredResult()
        {
            AmbiguousStripeCheckoutSeed? seededCheckout = null;
            var provider = new RecordingPaymentService(
                _ => Task.FromResult(new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Response lost",
                    FailureKind: PaymentInitializationFailureKind.Ambiguous)),
                async _ =>
                {
                    var seed = Assert.IsType<AmbiguousStripeCheckoutSeed>(seededCheckout);
                    var reconciliation = await CreateStripeReconciliationService().ReconcileAsync(
                        CreateStripeEvent(seed, "evt_paid_during_expiration") with
                        {
                            SessionId = "cs_recovery",
                            PaymentIntentId = "pi_recovery",
                        });
                    Assert.Equal(StripePaymentReconciliationOutcome.Processed, reconciliation);
                    return new StripePaymentRecoveryResult(
                        StripePaymentRecoveryOutcome.Expired,
                        "pi_recovery");
                });
            seededCheckout = await SeedAmbiguousStripeCheckoutAsync(provider);
            await ExpireProviderRecoveryDeadlineAsync(seededCheckout);

            var recovered = await ExecuteAsync(
                seededCheckout.Checkout,
                "customer-1",
                seededCheckout.Key,
                provider);

            Assert.True(recovered.Success);
            Assert.Equal(CheckoutStatus.Confirmed, recovered.Payload!.Status);
            Assert.Single(provider.Calls);
            Assert.Single(provider.RecoveryCalls);
            await AssertAmbiguousSeedStateAsync(
                seededCheckout,
                PaymentTransactionStatus.Paid,
                OrderPaymentStatus.Paid,
                InventoryReservationStatus.Consumed,
                expectedStock: 1,
                CheckoutIdempotencyState.Completed);
        }

        [Fact]
        public async Task InvalidPersistedProviderSnapshot_DoesNotTerminalizeUnknownPayment()
        {
            var seed = await SeedAmbiguousStripeCheckoutAsync();
            await using (var context = _database.CreateContext())
            {
                await context.CheckoutIdempotencyRecords.ExecuteUpdateAsync(setters => setters
                    .SetProperty(record => record.ProviderInitializationJson, "{}")
                    .SetProperty(record => record.LeaseExpiresOn, DateTime.UtcNow.AddSeconds(-1)));
            }

            var unresolved = await ExecuteAsync(seed.Checkout, "customer-1", seed.Key, seed.Provider);

            Assert.Equal(CheckoutExecutionStatus.InProgress, unresolved.Status);
            Assert.Single(seed.Provider.Calls);
            await AssertUnresolvedSeedStateAsync(seed);
        }

        [Fact]
        public async Task TerminalStripeFailure_ReplaysWithoutProviderOrInventoryWork()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            var key = Guid.NewGuid();
            var checkout = CreateCheckout(PaymentMethodIds.CreditCard, productId, quantity: 1);
            var provider = new RecordingPaymentService(_ => Task.FromResult(new PaymentInitializationResult(
                false,
                ErrorMessage: "Provider rejected session",
                FailureKind: PaymentInitializationFailureKind.Definitive)));

            var failed = await ExecuteAsync(checkout, "customer-1", key, provider);
            var replay = await ExecuteAsync(checkout, "customer-1", key, provider);

            Assert.False(failed.Success);
            Assert.False(replay.Success);
            Assert.True(replay.IsReplay);
            Assert.Equal(failed.Message, replay.Message);
            Assert.Single(provider.Calls);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(1, await assertionContext.Orders.CountAsync());
            Assert.Equal(2, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(
                PaymentTransactionStatus.Failed,
                (await assertionContext.PaymentTransactions.SingleAsync()).Status);
            Assert.Equal(
                OrderPaymentStatus.Failed,
                (await assertionContext.Orders.SingleAsync()).PaymentStatus);
            Assert.Equal(
                InventoryReservationStatus.Released,
                (await assertionContext.InventoryReservations.SingleAsync()).Status);
            Assert.Equal(
                CheckoutIdempotencyState.Failed,
                (await assertionContext.CheckoutIdempotencyRecords.SingleAsync()).State);
        }

        [Fact]
        public async Task GenericLocalFailureWithoutDecisionFence_IsRejected()
        {
            var seed = await SeedAmbiguousStripeCheckoutAsync();

            var transition = await CreateStripeTransitionService().TransitionAsync(
                seed.PaymentTransactionId,
                PaymentTransactionStatus.Failed);

            Assert.Equal(StripePaymentStateTransitionOutcome.Conflict, transition.Outcome);
            await AssertUnresolvedSeedStateAsync(seed);
        }

        [Theory]
        [InlineData(PaymentTransactionStatus.Failed, OrderPaymentStatus.Failed)]
        [InlineData(PaymentTransactionStatus.Cancelled, OrderPaymentStatus.Cancelled)]
        public async Task RetryAfterTerminalPaymentTransaction_DoesNotCallProviderOrMutateInventory(
            PaymentTransactionStatus paymentStatus,
            OrderPaymentStatus orderStatus)
        {
            var seed = await SeedAmbiguousStripeCheckoutAsync();
            if (paymentStatus == PaymentTransactionStatus.Failed)
            {
                Assert.Equal(
                    StripePaymentReconciliationOutcome.Processed,
                    await CreateStripeReconciliationService().ReconcileAsync(
                        CreateStripeEvent(
                            seed,
                            "evt_failed_before_checkout_retry",
                            "checkout.session.async_payment_failed",
                            paymentStatus: null)));
            }
            else
            {
                var transition = await CreateStripeTransitionService().TransitionAsync(
                    seed.PaymentTransactionId,
                    paymentStatus);
                Assert.Equal(StripePaymentStateTransitionOutcome.Applied, transition.Outcome);
            }
            await ExpireCheckoutLeaseAsync();

            var retry = await ExecuteAsync(seed.Checkout, "customer-1", seed.Key, seed.Provider);

            Assert.False(retry.Success);
            Assert.Single(seed.Provider.Calls);
            await using var context = _database.CreateContext();
            Assert.Single(await context.PaymentTransactions.ToListAsync());
            Assert.Equal(paymentStatus, (await context.PaymentTransactions.SingleAsync()).Status);
            Assert.Equal(orderStatus, (await context.Orders.SingleAsync()).PaymentStatus);
            Assert.Equal(InventoryReservationStatus.Released,
                (await context.InventoryReservations.SingleAsync()).Status);
            Assert.Equal(2, (await context.Products.FindAsync(seed.ProductId))!.Quantity);
            Assert.Equal(CheckoutIdempotencyState.Failed,
                (await context.CheckoutIdempotencyRecords.SingleAsync()).State);
        }

        [Fact]
        public async Task RecoveryAfterPaidPaymentTransaction_CompletesLocallyWithoutCallingProviderAgain()
        {
            var seed = await SeedAmbiguousStripeCheckoutAsync();
            var reconciliation = await CreateStripeReconciliationService().ReconcileAsync(
                CreateStripeEvent(seed, "evt_paid_before_checkout_recovery"));
            Assert.Equal(StripePaymentReconciliationOutcome.Processed, reconciliation);
            await ExpireCheckoutLeaseAsync();

            var retry = await ExecuteAsync(seed.Checkout, "customer-1", seed.Key, seed.Provider);

            Assert.True(retry.Success);
            Assert.Equal(CheckoutStatus.Confirmed, retry.Payload!.Status);
            Assert.Null(retry.Payload.RedirectUrl);
            Assert.Single(seed.Provider.Calls);
            await using var context = _database.CreateContext();
            Assert.Single(await context.PaymentTransactions.ToListAsync());
            Assert.Equal(PaymentTransactionStatus.Paid,
                (await context.PaymentTransactions.SingleAsync()).Status);
            Assert.Equal(OrderPaymentStatus.Paid, (await context.Orders.SingleAsync()).PaymentStatus);
            Assert.Equal(InventoryReservationStatus.Consumed,
                (await context.InventoryReservations.SingleAsync()).Status);
            Assert.Equal(1, (await context.Products.FindAsync(seed.ProductId))!.Quantity);
            Assert.Equal(CheckoutIdempotencyState.Completed,
                (await context.CheckoutIdempotencyRecords.SingleAsync()).State);
        }

        [Fact]
        public async Task CompletedRedirectReplayAfterPaidWebhook_ReturnsLocalPaidSuccess()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            var checkout = CreateCheckout(PaymentMethodIds.CreditCard, productId, quantity: 1);
            var provider = new RecordingPaymentService(_ => Task.FromResult(
                new PaymentInitializationResult(true, "https://checkout.stripe.test/completed")));
            var key = Guid.NewGuid();
            var initial = await ExecuteAsync(checkout, "customer-1", key, provider);
            Assert.True(initial.Success);
            Assert.Equal(CheckoutStatus.PendingPayment, initial.Payload!.Status);
            AmbiguousStripeCheckoutSeed seed;
            await using (var context = _database.CreateContext())
            {
                var order = await context.Orders.SingleAsync();
                var transaction = await context.PaymentTransactions.SingleAsync();
                seed = new AmbiguousStripeCheckoutSeed(
                    checkout,
                    key,
                    provider,
                    productId,
                    order.Id,
                    transaction.Id,
                    transaction.ExpectedAmountMinor,
                    transaction.Currency);
                Assert.Equal(
                    StripePaymentReconciliationOutcome.Processed,
                    await CreateStripeReconciliationService().ReconcileAsync(
                        CreateStripeEvent(seed, "evt_paid_after_redirect") with
                        {
                            SessionId = transaction.ProviderSessionId,
                            PaymentIntentId = transaction.ProviderPaymentIntentId,
                        }));
            }

            var replay = await ExecuteAsync(checkout, "customer-1", key, provider);

            Assert.True(replay.Success);
            Assert.True(replay.IsReplay);
            Assert.Equal(CheckoutStatus.Confirmed, replay.Payload!.Status);
            Assert.Null(replay.Payload.RedirectUrl);
            Assert.Single(provider.Calls);
        }

        [Theory]
        [InlineData("checkout.session.completed", "paid", PaymentTransactionStatus.Paid, true)]
        [InlineData("checkout.session.async_payment_failed", null, PaymentTransactionStatus.Failed, false)]
        [InlineData("checkout.session.expired", "unpaid", PaymentTransactionStatus.Cancelled, false)]
        public async Task WebhookTerminalizingBeforeProviderIdentityPersistence_DecidesCheckoutOutcome(
            string eventType,
            string? paymentStatus,
            PaymentTransactionStatus expectedPaymentStatus,
            bool expectedSuccess)
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            var checkout = CreateCheckout(PaymentMethodIds.CreditCard, productId, quantity: 1);
            var provider = new RecordingPaymentService(_ => Task.FromResult(
                new PaymentInitializationResult(true, "https://checkout.stripe.test/race")));
            var eventId = $"evt_identity_{expectedPaymentStatus}";
            var store = new ReconcileBeforeIdentityPersistenceStore(
                new PaymentTransactionStore(_database.CreateContextFactory()),
                async (paymentTransactionId, sessionId, paymentIntentId) =>
                {
                    await using var context = _database.CreateContext();
                    var transaction = await context.PaymentTransactions
                        .AsNoTracking()
                        .SingleAsync(item => item.Id == paymentTransactionId);
                    var seed = new AmbiguousStripeCheckoutSeed(
                        checkout,
                        Guid.Empty,
                        provider,
                        productId,
                        transaction.OrderId,
                        transaction.Id,
                        transaction.ExpectedAmountMinor,
                        transaction.Currency);
                    var outcome = await CreateStripeReconciliationService().ReconcileAsync(
                        CreateStripeEvent(seed, eventId, eventType, paymentStatus) with
                        {
                            SessionId = sessionId,
                            PaymentIntentId = paymentIntentId,
                        });
                    Assert.Equal(StripePaymentReconciliationOutcome.Processed, outcome);
                });

            var result = await ExecuteAsync(
                checkout,
                "customer-1",
                Guid.NewGuid(),
                provider,
                paymentTransactionStore: store);

            Assert.Equal(expectedSuccess, result.Success);
            Assert.Single(provider.Calls);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(expectedPaymentStatus,
                (await assertionContext.PaymentTransactions.SingleAsync()).Status);
            Assert.Equal(
                expectedSuccess ? CheckoutIdempotencyState.Completed : CheckoutIdempotencyState.Failed,
                (await assertionContext.CheckoutIdempotencyRecords.SingleAsync()).State);
            if (expectedSuccess)
            {
                Assert.Equal(CheckoutStatus.Confirmed, result.Payload!.Status);
                Assert.Null(result.Payload.RedirectUrl);
                Assert.Equal(InventoryReservationStatus.Consumed,
                    (await assertionContext.InventoryReservations.SingleAsync()).Status);
                Assert.Equal(1, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            }
            else
            {
                Assert.Equal(InventoryReservationStatus.Released,
                    (await assertionContext.InventoryReservations.SingleAsync()).Status);
                Assert.Equal(2, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            }
        }

        [Theory]
        [InlineData(PaymentTransactionStatus.Failed)]
        [InlineData(PaymentTransactionStatus.Cancelled)]
        public async Task PaidWebhookWinningAgainstLocalTerminalization_CannotPersistCheckoutFailure(
            PaymentTransactionStatus localTarget)
        {
            var seed = await SeedAmbiguousStripeCheckoutAsync();
            var authorization = localTarget == PaymentTransactionStatus.Failed
                ? await CreateDefinitiveFailureAuthorizationAsync(seed)
                : null;
            var webhookLock = new PaymentTransactionLockInterceptor();
            var reconciliationTask = CreateStripeReconciliationService(webhookLock).ReconcileAsync(
                CreateStripeEvent(seed, $"evt_paid_wins_{localTarget}"));
            await webhookLock.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var transitionService = CreateStripeTransitionService();
            var compensationTask = authorization is null
                ? transitionService.TransitionAsync(seed.PaymentTransactionId, localTarget)
                : transitionService.TransitionDefinitiveInitialFailureAsync(
                    seed.PaymentTransactionId,
                    authorization.CheckoutIdempotencyRecordId,
                    authorization.LeaseOwnerId,
                    authorization.DecisionId);
            webhookLock.Release.SetResult();

            Assert.Equal(StripePaymentReconciliationOutcome.Processed, await reconciliationTask);
            var compensation = await compensationTask;
            Assert.Equal(StripePaymentStateTransitionOutcome.AlreadyTerminal, compensation.Outcome);
            Assert.Equal(PaymentTransactionStatus.Paid, compensation.PaymentStatus);
            await ExpireCheckoutLeaseAsync();
            var retry = await ExecuteAsync(seed.Checkout, "customer-1", seed.Key, seed.Provider);

            Assert.True(retry.Success);
            Assert.Equal(CheckoutStatus.Confirmed, retry.Payload!.Status);
            await AssertAmbiguousSeedStateAsync(
                seed,
                PaymentTransactionStatus.Paid,
                OrderPaymentStatus.Paid,
                InventoryReservationStatus.Consumed,
                expectedStock: 1,
                CheckoutIdempotencyState.Completed);
        }

        [Fact]
        public async Task LocalFailureWinningAgainstPaidWebhook_LeavesConsistentFailureAndRejectsEvent()
        {
            var seed = await SeedAmbiguousStripeCheckoutAsync();
            var authorization = await CreateDefinitiveFailureAuthorizationAsync(seed);
            var localLock = new PaymentTransactionLockInterceptor();
            var compensationTask = CreateStripeTransitionService(localLock)
                .TransitionDefinitiveInitialFailureAsync(
                seed.PaymentTransactionId,
                authorization.CheckoutIdempotencyRecordId,
                authorization.LeaseOwnerId,
                authorization.DecisionId);
            await localLock.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var reconciliationTask = CreateStripeReconciliationService().ReconcileAsync(
                CreateStripeEvent(seed, "evt_local_failure_wins"));
            localLock.Release.SetResult();

            Assert.Equal(StripePaymentStateTransitionOutcome.Applied, (await compensationTask).Outcome);
            Assert.Equal(StripePaymentReconciliationOutcome.Rejected, await reconciliationTask);
            await ExpireCheckoutLeaseAsync();
            var retry = await ExecuteAsync(seed.Checkout, "customer-1", seed.Key, seed.Provider);

            Assert.False(retry.Success);
            await AssertAmbiguousSeedStateAsync(
                seed,
                PaymentTransactionStatus.Failed,
                OrderPaymentStatus.Failed,
                InventoryReservationStatus.Released,
                expectedStock: 2,
                CheckoutIdempotencyState.Failed);
            await using var context = _database.CreateContext();
            Assert.Equal(
                PaymentProviderEventOutcome.Rejected,
                (await context.PaymentProviderEvents.SingleAsync()).ProcessingOutcome);
        }

        [Fact]
        public async Task WebhookFailureRacingLocalFailure_RestoresStockExactlyOnce()
        {
            var seed = await SeedAmbiguousStripeCheckoutAsync();
            var authorization = await CreateDefinitiveFailureAuthorizationAsync(seed);
            var webhookLock = new PaymentTransactionLockInterceptor();
            var reconciliationTask = CreateStripeReconciliationService(webhookLock).ReconcileAsync(
                CreateStripeEvent(
                    seed,
                    "evt_webhook_failure_wins",
                    "checkout.session.async_payment_failed",
                    paymentStatus: null));
            await webhookLock.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var compensationTask = CreateStripeTransitionService()
                .TransitionDefinitiveInitialFailureAsync(
                seed.PaymentTransactionId,
                authorization.CheckoutIdempotencyRecordId,
                authorization.LeaseOwnerId,
                authorization.DecisionId);
            webhookLock.Release.SetResult();

            Assert.Equal(StripePaymentReconciliationOutcome.Processed, await reconciliationTask);
            var compensation = await compensationTask;
            Assert.Equal(StripePaymentStateTransitionOutcome.AlreadyTerminal, compensation.Outcome);
            Assert.Equal(PaymentTransactionStatus.Failed, compensation.PaymentStatus);
            await using var context = _database.CreateContext();
            Assert.Equal(PaymentTransactionStatus.Failed,
                (await context.PaymentTransactions.SingleAsync()).Status);
            Assert.Equal(OrderPaymentStatus.Failed, (await context.Orders.SingleAsync()).PaymentStatus);
            Assert.Equal(InventoryReservationStatus.Released,
                (await context.InventoryReservations.SingleAsync()).Status);
            Assert.Equal(2, (await context.Products.FindAsync(seed.ProductId))!.Quantity);
        }

        [Fact]
        public async Task ReorderedAndGroupedEquivalentLines_ReplayOriginalOutcome()
        {
            await _database.ResetDatabaseAsync();
            var firstProductId = await SeedProductAsync(quantity: 5);
            var secondProductId = await SeedProductAsync(quantity: 5);
            var key = Guid.NewGuid();
            var first = new Checkout
            {
                PaymentMethodId = PaymentMethodIds.CashOnDelivery,
                Carts =
                [
                    new CartLineRequest(firstProductId, null, 1),
                    new CartLineRequest(secondProductId, null, 1),
                    new CartLineRequest(firstProductId, null, 2),
                ],
            };
            var equivalent = new Checkout
            {
                PaymentMethodId = PaymentMethodIds.CashOnDelivery,
                Carts =
                [
                    new CartLineRequest(secondProductId, null, 1),
                    new CartLineRequest(firstProductId, null, 3),
                ],
            };

            var original = await ExecuteAsync(first, "customer-1", key);
            var replay = await ExecuteAsync(equivalent, "customer-1", key);

            Assert.True(replay.IsReplay);
            Assert.Equal(original.Payload!.OrderId, replay.Payload!.OrderId);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(1, await assertionContext.Orders.CountAsync());
            Assert.Equal(2, await assertionContext.InventoryReservations.CountAsync());
            Assert.Equal(2, (await assertionContext.Products.FindAsync(firstProductId))!.Quantity);
            Assert.Equal(4, (await assertionContext.Products.FindAsync(secondProductId))!.Quantity);
        }

        [Fact]
        public async Task OpportunisticRetentionCleanup_DeletesOnlyExpiredTerminalRecords()
        {
            await _database.ResetDatabaseAsync();
            var expiredTerminalId = Guid.NewGuid();
            var activeId = Guid.NewGuid();
            await using (var seedContext = _database.CreateContext())
            {
                seedContext.CheckoutIdempotencyRecords.AddRange(
                    new CheckoutIdempotencyRecord
                    {
                        Id = expiredTerminalId,
                        UserId = "customer-1",
                        IdempotencyKey = Guid.NewGuid(),
                        RequestFingerprint = new string('a', 64),
                        PaymentMethodId = PaymentMethodIds.CashOnDelivery,
                        State = CheckoutIdempotencyState.Completed,
                        OrderId = Guid.NewGuid(),
                        OrderReference = "COD-EXPIRED",
                        CompletedOn = DateTime.UtcNow.AddDays(-15),
                        ExpiresOn = DateTime.UtcNow.AddDays(-1),
                    },
                    new CheckoutIdempotencyRecord
                    {
                        Id = activeId,
                        UserId = "customer-1",
                        IdempotencyKey = Guid.NewGuid(),
                        RequestFingerprint = new string('b', 64),
                        PaymentMethodId = PaymentMethodIds.CashOnDelivery,
                        State = CheckoutIdempotencyState.Processing,
                        OrderId = Guid.NewGuid(),
                        OrderReference = "COD-ACTIVE",
                        LeaseOwnerId = Guid.NewGuid(),
                        LeaseExpiresOn = DateTime.UtcNow.AddMinutes(1),
                        ExpiresOn = DateTime.UtcNow.AddDays(-1),
                    });
                await seedContext.SaveChangesAsync();
            }

            await CreateStore().ClaimAsync(
                "customer-2",
                Guid.NewGuid(),
                new string('c', 64),
                PaymentMethodIds.CashOnDelivery);

            await using var assertionContext = _database.CreateContext();
            Assert.Null(await assertionContext.CheckoutIdempotencyRecords.FindAsync(expiredTerminalId));
            Assert.NotNull(await assertionContext.CheckoutIdempotencyRecords.FindAsync(activeId));
        }

        private async Task<AmbiguousStripeCheckoutSeed> SeedAmbiguousStripeCheckoutAsync(
            RecordingPaymentService? paymentProvider = null)
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            var key = Guid.NewGuid();
            var checkout = CreateCheckout(PaymentMethodIds.CreditCard, productId, quantity: 1);
            var provider = paymentProvider ?? new RecordingPaymentService(_ => Task.FromResult(
                new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Response lost",
                    FailureKind: PaymentInitializationFailureKind.Ambiguous)));

            var ambiguous = await ExecuteAsync(checkout, "customer-1", key, provider);

            Assert.Equal(CheckoutExecutionStatus.InProgress, ambiguous.Status);
            await using var context = _database.CreateContext();
            var order = await context.Orders.SingleAsync();
            var paymentTransaction = await context.PaymentTransactions.SingleAsync();
            return new AmbiguousStripeCheckoutSeed(
                checkout,
                key,
                provider,
                productId,
                order.Id,
                paymentTransaction.Id,
                paymentTransaction.ExpectedAmountMinor,
                paymentTransaction.Currency);
        }

        private async Task ExpireCheckoutLeaseAsync()
        {
            await using var context = _database.CreateContext();
            await context.CheckoutIdempotencyRecords.ExecuteUpdateAsync(setters => setters
                .SetProperty(record => record.LeaseExpiresOn, DateTime.UtcNow.AddSeconds(-1)));
        }

        private async Task ExpireProviderRecoveryDeadlineAsync(
            AmbiguousStripeCheckoutSeed seed,
            string providerSessionId = "cs_recovery",
            string? providerPaymentIntentId = "pi_recovery")
        {
            await using var context = _database.CreateContext();
            await context.PaymentTransactions
                .Where(transaction => transaction.Id == seed.PaymentTransactionId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(transaction => transaction.ProviderSessionId, providerSessionId)
                    .SetProperty(transaction => transaction.ProviderPaymentIntentId, providerPaymentIntentId));
            await context.CheckoutIdempotencyRecords.ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    record => record.ProviderInitializationStartedOn,
                    DateTime.UtcNow.AddHours(-24))
                .SetProperty(record => record.LeaseExpiresOn, DateTime.UtcNow.AddSeconds(-1)));
        }

        private async Task<DefinitiveFailureAuthorization> CreateDefinitiveFailureAuthorizationAsync(
            AmbiguousStripeCheckoutSeed seed)
        {
            string fingerprint;
            await using (var context = _database.CreateContext())
            {
                fingerprint = (await context.CheckoutIdempotencyRecords.SingleAsync()).RequestFingerprint;
            }

            var store = CreateStore();
            var claim = await store.ClaimAsync(
                "customer-1",
                seed.Key,
                fingerprint,
                PaymentMethodIds.CreditCard);
            Assert.Equal(CheckoutIdempotencyClaimStatus.Acquired, claim.Status);
            var decisionId = Guid.NewGuid();
            var failure = new PersistedCheckoutOutcome(
                1,
                CheckoutExecutionStatus.BadRequest,
                new ServiceResponse<CheckoutResult>(false, "Definitive initial rejection."),
                decisionId);
            Assert.True(await store.SetPendingOutcomeAsync(
                claim.Record.Id,
                claim.LeaseOwnerId,
                failure));
            return new DefinitiveFailureAuthorization(
                claim.Record.Id,
                claim.LeaseOwnerId,
                decisionId);
        }

        private StripePaymentStateTransitionService CreateStripeTransitionService(
            params IInterceptor[] interceptors) => new(
                _database.CreateContextFactory(interceptors),
                Mock.Of<ILogger<StripePaymentStateTransitionService>>());

        private StripePaymentReconciliationService CreateStripeReconciliationService(
            params IInterceptor[] interceptors) => new(
                _database.CreateContextFactory(interceptors),
                Mock.Of<ILogger<StripePaymentReconciliationService>>());

        private static StripeWebhookEventData CreateStripeEvent(
            AmbiguousStripeCheckoutSeed seed,
            string eventId,
            string eventType = "checkout.session.completed",
            string? paymentStatus = "paid") => new(
                eventId,
                eventType,
                DateTime.UtcNow,
                "cs_race_test",
                "pi_race_test",
                seed.OrderId,
                seed.OrderId.ToString("D"),
                seed.PaymentTransactionId,
                paymentStatus,
                seed.ExpectedAmountMinor,
                seed.Currency.ToLowerInvariant(),
                "complete");

        private async Task AssertAmbiguousSeedStateAsync(
            AmbiguousStripeCheckoutSeed seed,
            PaymentTransactionStatus paymentStatus,
            OrderPaymentStatus orderStatus,
            InventoryReservationStatus reservationStatus,
            int expectedStock,
            CheckoutIdempotencyState idempotencyState)
        {
            await using var context = _database.CreateContext();
            Assert.Equal(paymentStatus,
                (await context.PaymentTransactions.FindAsync(seed.PaymentTransactionId))!.Status);
            Assert.Equal(orderStatus, (await context.Orders.FindAsync(seed.OrderId))!.PaymentStatus);
            Assert.Equal(reservationStatus,
                (await context.InventoryReservations.SingleAsync()).Status);
            Assert.Equal(expectedStock, (await context.Products.FindAsync(seed.ProductId))!.Quantity);
            Assert.Equal(idempotencyState,
                (await context.CheckoutIdempotencyRecords.SingleAsync()).State);
            Assert.Single(await context.PaymentTransactions.ToListAsync());
        }

        private async Task AssertUnresolvedSeedStateAsync(AmbiguousStripeCheckoutSeed seed)
        {
            await using var context = _database.CreateContext();
            Assert.Equal(
                PaymentTransactionStatus.Pending,
                (await context.PaymentTransactions.FindAsync(seed.PaymentTransactionId))!.Status);
            Assert.Equal(OrderPaymentStatus.Pending, (await context.Orders.FindAsync(seed.OrderId))!.PaymentStatus);
            Assert.Equal(
                InventoryReservationStatus.Reserved,
                (await context.InventoryReservations.SingleAsync()).Status);
            Assert.Equal(1, (await context.Products.FindAsync(seed.ProductId))!.Quantity);
            var record = await context.CheckoutIdempotencyRecords.SingleAsync();
            Assert.Equal(CheckoutIdempotencyState.LocalCommitted, record.State);
            Assert.NotNull(record.OutcomeJson);
            Assert.Contains("requires reconciliation", record.OutcomeJson, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(seed.Key, record.IdempotencyKey);
        }

        private async Task<CheckoutExecutionResult> ExecuteAsync(
            Checkout checkout,
            string userId,
            Guid key,
            IPaymentService? payment = null,
            IEmailService? email = null,
            IPaymentTransactionStore? paymentTransactionStore = null,
            ICheckoutIdempotencyStore? idempotencyStore = null,
            IStripePaymentStateTransitionService? stripeTransitionService = null)
        {
            await using var context = _database.CreateContext();
            var paymentMethods = new Mock<IPaymentMethodService>();
            paymentMethods.Setup(service => service.GetPaymentMethodsAsync())
                .ReturnsAsync([new GetPaymentMethod { Id = checkout.PaymentMethodId, Name = "Available" }]);
            var users = new Mock<IAppUserManager>();
            users.Setup(manager => manager.GetUserByIdAsync(userId))
                .ReturnsAsync(new AppUser { Id = userId, Email = $"{userId}@example.com" });
            var orchestrator = new CheckoutOrchestrator(
                new ProductReadRepository(context),
                paymentMethods.Object,
                payment ?? Mock.Of<IPaymentService>(),
                users.Object,
                new InventoryReservationService(_database.CreateContextFactory()),
                idempotencyStore ?? CreateStore(),
                paymentTransactionStore ?? new PaymentTransactionStore(_database.CreateContextFactory()),
                stripeTransitionService ?? CreateStripeTransitionService(),
                new OrderRepository(context),
                email ?? Mock.Of<IEmailService>(),
                Options.Create(new BankTransferSettings
                {
                    Iban = "BG00TEST",
                    Beneficiary = "Blazor Shop",
                    BankName = "Test Bank",
                }),
                Options.Create(new ClientAppOptions { BaseUrl = "https://shop.test" }),
                Options.Create(new CheckoutIdempotencyOptions
                {
                    ProviderRecoveryWindowHours = 23,
                }),
                Options.Create(new CommerceOptions { Currency = "EUR" }),
                Mock.Of<ILogger<CheckoutOrchestrator>>());
            return await orchestrator.CheckoutAsync(checkout, userId, key);
        }

        private CheckoutIdempotencyStore CreateStore(params IInterceptor[] interceptors) => new(
            _database.CreateContextFactory(interceptors),
            Options.Create(new CheckoutIdempotencyOptions
            {
                RetentionDays = 14,
                LeaseSeconds = 5,
                DuplicateWaitMilliseconds = 5000,
                PollMilliseconds = 20,
                ProviderRecoveryWindowHours = 23,
            }));

        private async Task<Guid> SeedProductAsync(int quantity, decimal price = 10m)
        {
            await using var context = _database.CreateContext();
            var category = await context.Categories.FirstOrDefaultAsync();
            if (category is null)
            {
                category = new Category { Id = Guid.NewGuid(), Name = "Idempotency" };
                context.Categories.Add(category);
            }

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Product-{Guid.NewGuid():N}",
                Price = price,
                Quantity = quantity,
                CategoryId = category.Id,
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            return product.Id;
        }

        private async Task<(Guid ProductId, Guid VariantId)> SeedVariantProductAsync(int stock)
        {
            await using var context = _database.CreateContext();
            var category = await context.Categories.FirstOrDefaultAsync();
            if (category is null)
            {
                category = new Category { Id = Guid.NewGuid(), Name = "Idempotency" };
                context.Categories.Add(category);
            }

            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"VariantProduct-{Guid.NewGuid():N}",
                Price = 5m,
                Quantity = 0,
                CategoryId = category.Id,
            };
            var variant = new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Sku = "TEST-VARIANT",
                SizeScale = SizeScale.ShoesUS,
                SizeValue = "10",
                Price = 25m,
                Stock = stock,
                Color = "Black",
            };
            context.AddRange(product, variant);
            await context.SaveChangesAsync();
            return (product.Id, variant.Id);
        }

        private static Checkout CreateCheckout(Guid paymentMethodId, Guid productId, int quantity) => new()
        {
            PaymentMethodId = paymentMethodId,
            Carts = [new CartLineRequest(productId, null, quantity)],
        };

        private static async Task<T[]> RunConcurrentlyAsync<T>(
            Func<Task<T>> first,
            Func<Task<T>> second)
        {
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<T> RunAsync(Func<Task<T>> operation)
            {
                await start.Task;
                return await operation();
            }

            var firstTask = RunAsync(first);
            var secondTask = RunAsync(second);
            start.SetResult();
            return await Task.WhenAll(firstTask, secondTask);
        }

        private sealed class RecordingPaymentService : IPaymentService
        {
            private readonly Func<ProviderCall, Task<PaymentInitializationResult>> _handler;
            private readonly Func<StripePaymentRecoveryRequest, Task<StripePaymentRecoveryResult>> _recoveryHandler;

            public RecordingPaymentService(
                Func<ProviderCall, Task<PaymentInitializationResult>> handler,
                Func<StripePaymentRecoveryRequest, Task<StripePaymentRecoveryResult>>? recoveryHandler = null)
            {
                _handler = handler;
                _recoveryHandler = recoveryHandler
                    ?? (_ => Task.FromResult(new StripePaymentRecoveryResult(
                        StripePaymentRecoveryOutcome.Unresolved,
                        Reason: "No provider state was configured.")));
            }

            public ConcurrentQueue<ProviderCall> Calls { get; } = new();

            public ConcurrentQueue<StripePaymentRecoveryRequest> RecoveryCalls { get; } = new();

            public async Task<PaymentInitializationResult> Pay(
                StripeCheckoutInitialization initialization,
                string providerIdempotencyKey,
                CancellationToken cancellationToken = default)
            {
                var call = new ProviderCall(initialization, providerIdempotencyKey);
                Calls.Enqueue(call);
                var result = await _handler(call);
                return result.Success && string.IsNullOrWhiteSpace(result.ProviderSessionId)
                    ? result with
                    {
                        ProviderSessionId = $"cs_{providerIdempotencyKey}",
                        ProviderPaymentIntentId = $"pi_{providerIdempotencyKey}",
                    }
                    : result;
            }

            public Task<StripePaymentRecoveryResult> RecoverAsync(
                StripePaymentRecoveryRequest request,
                CancellationToken cancellationToken = default)
            {
                RecoveryCalls.Enqueue(request);
                return _recoveryHandler(request);
            }
        }

        private sealed record ProviderCall(
            StripeCheckoutInitialization Initialization,
            string ProviderIdempotencyKey);

        private sealed record AmbiguousStripeCheckoutSeed(
            Checkout Checkout,
            Guid Key,
            RecordingPaymentService Provider,
            Guid ProductId,
            Guid OrderId,
            Guid PaymentTransactionId,
            long ExpectedAmountMinor,
            string Currency);

        private sealed record DefinitiveFailureAuthorization(
            Guid CheckoutIdempotencyRecordId,
            Guid LeaseOwnerId,
            Guid DecisionId);

        private sealed class ProviderPreparationBarrierStore : ICheckoutIdempotencyStore
        {
            private readonly ICheckoutIdempotencyStore _inner;

            public ProviderPreparationBarrierStore(ICheckoutIdempotencyStore inner)
            {
                _inner = inner;
            }

            public TaskCompletionSource PreparationReached { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource ResumePreparation { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<CheckoutIdempotencyClaim> ClaimAsync(
                string userId,
                Guid idempotencyKey,
                string requestFingerprint,
                Guid paymentMethodId,
                CancellationToken cancellationToken = default) =>
                _inner.ClaimAsync(
                    userId,
                    idempotencyKey,
                    requestFingerprint,
                    paymentMethodId,
                    cancellationToken);

            public Task<bool> SetPendingOutcomeAsync(
                Guid recordId,
                Guid leaseOwnerId,
                PersistedCheckoutOutcome outcome,
                CancellationToken cancellationToken = default) =>
                _inner.SetPendingOutcomeAsync(recordId, leaseOwnerId, outcome, cancellationToken);

            public async Task<CheckoutProviderInitialization?> PrepareProviderInitializationAsync(
                Guid recordId,
                Guid leaseOwnerId,
                StripeCheckoutInitialization proposedInitialization,
                CancellationToken cancellationToken = default)
            {
                PreparationReached.TrySetResult();
                await ResumePreparation.Task.WaitAsync(cancellationToken);
                return await _inner.PrepareProviderInitializationAsync(
                    recordId,
                    leaseOwnerId,
                    proposedInitialization,
                    cancellationToken);
            }

            public Task<bool> CompleteAsync(
                Guid recordId,
                Guid leaseOwnerId,
                PersistedCheckoutOutcome outcome,
                CheckoutIdempotencyState terminalState,
                CancellationToken cancellationToken = default) =>
                _inner.CompleteAsync(recordId, leaseOwnerId, outcome, terminalState, cancellationToken);

            public Task<bool> ReleaseLeaseAsync(
                Guid recordId,
                Guid leaseOwnerId,
                CancellationToken cancellationToken = default) =>
                _inner.ReleaseLeaseAsync(recordId, leaseOwnerId, cancellationToken);

            public Task<CheckoutIdempotencyRecord?> GetAsync(
                Guid recordId,
                CancellationToken cancellationToken = default) =>
                _inner.GetAsync(recordId, cancellationToken);

            public PersistedCheckoutOutcome? ReadOutcome(CheckoutIdempotencyRecord record) =>
                _inner.ReadOutcome(record);
        }

        private sealed class PreAcquisitionReadBarrierInterceptor : DbCommandInterceptor
        {
            private int _remainingBlocks = 1;

            public TaskCompletionSource ReadCompleted { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource ResumeAcquisition { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public override async ValueTask<DbDataReader> ReaderExecutedAsync(
                DbCommand command,
                CommandExecutedEventData eventData,
                DbDataReader result,
                CancellationToken cancellationToken = default)
            {
                if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)
                    && command.CommandText.Contains("CheckoutIdempotencyRecords", StringComparison.Ordinal)
                    && Interlocked.Exchange(ref _remainingBlocks, 0) == 1)
                {
                    ReadCompleted.TrySetResult();
                    await ResumeAcquisition.Task.WaitAsync(cancellationToken);
                }

                return result;
            }
        }

        private sealed class PauseBeforeDefinitiveFailureTransitionService
            : IStripePaymentStateTransitionService
        {
            private readonly IStripePaymentStateTransitionService _inner;

            public PauseBeforeDefinitiveFailureTransitionService(
                IStripePaymentStateTransitionService inner)
            {
                _inner = inner;
            }

            public TaskCompletionSource TransitionReached { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource ResumeTransition { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<StripePaymentStateTransitionResult> TransitionAsync(
                Guid paymentTransactionId,
                PaymentTransactionStatus targetStatus,
                CancellationToken cancellationToken = default) =>
                _inner.TransitionAsync(paymentTransactionId, targetStatus, cancellationToken);

            public async Task<StripePaymentStateTransitionResult>
                TransitionDefinitiveInitialFailureAsync(
                    Guid paymentTransactionId,
                    Guid checkoutIdempotencyRecordId,
                    Guid leaseOwnerId,
                    Guid definitiveInitialRejectionId,
                    CancellationToken cancellationToken = default)
            {
                TransitionReached.TrySetResult();
                await ResumeTransition.Task.WaitAsync(cancellationToken);
                return await _inner.TransitionDefinitiveInitialFailureAsync(
                    paymentTransactionId,
                    checkoutIdempotencyRecordId,
                    leaseOwnerId,
                    definitiveInitialRejectionId,
                    cancellationToken);
            }
        }

        private sealed class CrashBeforeDefinitiveFailureTransitionService
            : IStripePaymentStateTransitionService
        {
            public Task<StripePaymentStateTransitionResult> TransitionAsync(
                Guid paymentTransactionId,
                PaymentTransactionStatus targetStatus,
                CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("Only definitive failure was expected.");

            public Task<StripePaymentStateTransitionResult> TransitionDefinitiveInitialFailureAsync(
                Guid paymentTransactionId,
                Guid checkoutIdempotencyRecordId,
                Guid leaseOwnerId,
                Guid definitiveInitialRejectionId,
                CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("Simulated process crash before terminal mutation.");
        }

        private sealed class PaymentTransactionLockInterceptor : DbCommandInterceptor
        {
            private int _remainingBlocks = 1;

            public TaskCompletionSource LockAcquired { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource Release { get; } = new(
                TaskCreationOptions.RunContinuationsAsynchronously);

            public override async ValueTask<DbDataReader> ReaderExecutedAsync(
                DbCommand command,
                CommandExecutedEventData eventData,
                DbDataReader result,
                CancellationToken cancellationToken = default)
            {
                if (command.CommandText.Contains("PaymentTransactions", StringComparison.Ordinal)
                    && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal)
                    && Interlocked.Exchange(ref _remainingBlocks, 0) == 1)
                {
                    LockAcquired.TrySetResult();
                    await Release.Task.WaitAsync(cancellationToken);
                }

                return result;
            }
        }

        private sealed class ReconcileBeforeIdentityPersistenceStore : IPaymentTransactionStore
        {
            private readonly IPaymentTransactionStore _inner;
            private readonly Func<Guid, string, string?, Task> _beforePersistence;

            public ReconcileBeforeIdentityPersistenceStore(
                IPaymentTransactionStore inner,
                Func<Guid, string, string?, Task> beforePersistence)
            {
                _inner = inner;
                _beforePersistence = beforePersistence;
            }

            public Task<PaymentTransaction> GetOrCreateStripeAsync(
                Guid orderId,
                long expectedAmountMinor,
                string currency,
                CancellationToken cancellationToken = default) =>
                _inner.GetOrCreateStripeAsync(orderId, expectedAmountMinor, currency, cancellationToken);

            public Task<PaymentTransaction?> GetStripeByOrderIdAsync(
                Guid orderId,
                CancellationToken cancellationToken = default) =>
                _inner.GetStripeByOrderIdAsync(orderId, cancellationToken);

            public async Task<PaymentProviderIdentityPersistenceResult> PersistProviderIdentityAsync(
                Guid paymentTransactionId,
                string providerSessionId,
                string? providerPaymentIntentId,
                CancellationToken cancellationToken = default)
            {
                await _beforePersistence(
                    paymentTransactionId,
                    providerSessionId,
                    providerPaymentIntentId);
                return await _inner.PersistProviderIdentityAsync(
                    paymentTransactionId,
                    providerSessionId,
                    providerPaymentIntentId,
                    cancellationToken);
            }
        }

        private sealed class FailFirstIdentityPersistenceStore : IPaymentTransactionStore
        {
            private readonly IPaymentTransactionStore _inner;
            private int _attempts;

            public FailFirstIdentityPersistenceStore(IPaymentTransactionStore inner)
            {
                _inner = inner;
            }

            public Task<PaymentTransaction> GetOrCreateStripeAsync(
                Guid orderId,
                long expectedAmountMinor,
                string currency,
                CancellationToken cancellationToken = default) =>
                _inner.GetOrCreateStripeAsync(orderId, expectedAmountMinor, currency, cancellationToken);

            public Task<PaymentTransaction?> GetStripeByOrderIdAsync(
                Guid orderId,
                CancellationToken cancellationToken = default) =>
                _inner.GetStripeByOrderIdAsync(orderId, cancellationToken);

            public Task<PaymentProviderIdentityPersistenceResult> PersistProviderIdentityAsync(
                Guid paymentTransactionId,
                string providerSessionId,
                string? providerPaymentIntentId,
                CancellationToken cancellationToken = default)
            {
                if (Interlocked.Increment(ref _attempts) == 1)
                {
                    throw new TimeoutException("Simulated lost database response.");
                }

                return _inner.PersistProviderIdentityAsync(
                    paymentTransactionId,
                    providerSessionId,
                    providerPaymentIntentId,
                    cancellationToken);
            }
        }
    }
}
