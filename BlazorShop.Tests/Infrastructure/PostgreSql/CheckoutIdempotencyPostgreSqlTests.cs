namespace BlazorShop.Tests.Infrastructure.PostgreSql
{
    using System.Collections.Concurrent;
    using System.Text.Json;

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
                Status = "Pending",
                Reference = claim.Record.OrderReference,
                TotalAmount = 10m,
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

        [Fact]
        public async Task StripeRecoveryAfterProviderWindow_StopsProviderAndReleasesInventoryOnce()
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

            var expired = await ExecuteAsync(checkout, "customer-1", key, provider);
            var replay = await ExecuteAsync(checkout, "customer-1", key, provider);

            Assert.False(expired.Success);
            Assert.Contains("recovery window expired", expired.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(replay.Success);
            Assert.True(replay.IsReplay);
            Assert.Single(provider.Calls);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(2, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(PaymentOrderStatus.PaymentFailed, (await assertionContext.Orders.SingleAsync()).Status);
            Assert.Equal(
                InventoryReservationStatus.Released,
                (await assertionContext.InventoryReservations.SingleAsync()).Status);
            var record = await assertionContext.CheckoutIdempotencyRecords.SingleAsync();
            Assert.Equal(CheckoutIdempotencyState.Failed, record.State);
            Assert.NotNull(record.ProviderInitializationStartedOn);
            Assert.NotNull(record.ProviderInitializationJson);
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
                InventoryReservationStatus.Released,
                (await assertionContext.InventoryReservations.SingleAsync()).Status);
            Assert.Equal(
                CheckoutIdempotencyState.Failed,
                (await assertionContext.CheckoutIdempotencyRecords.SingleAsync()).State);
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

        private async Task<CheckoutExecutionResult> ExecuteAsync(
            Checkout checkout,
            string userId,
            Guid key,
            IPaymentService? payment = null,
            IEmailService? email = null)
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
                CreateStore(),
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
                Mock.Of<ILogger<CheckoutOrchestrator>>());
            return await orchestrator.CheckoutAsync(checkout, userId, key);
        }

        private CheckoutIdempotencyStore CreateStore() => new(
            _database.CreateContextFactory(),
            Options.Create(new CheckoutIdempotencyOptions
            {
                RetentionDays = 14,
                LeaseSeconds = 5,
                DuplicateWaitMilliseconds = 5000,
                PollMilliseconds = 20,
                ProviderRecoveryWindowHours = 23,
            }));

        private async Task<Guid> SeedProductAsync(int quantity)
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
                Price = 10m,
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

            public RecordingPaymentService(Func<ProviderCall, Task<PaymentInitializationResult>> handler)
            {
                _handler = handler;
            }

            public ConcurrentQueue<ProviderCall> Calls { get; } = new();

            public async Task<PaymentInitializationResult> Pay(
                StripeCheckoutInitialization initialization,
                string providerIdempotencyKey,
                CancellationToken cancellationToken = default)
            {
                var call = new ProviderCall(initialization, providerIdempotencyKey);
                Calls.Enqueue(call);
                return await _handler(call);
            }
        }

        private sealed record ProviderCall(
            StripeCheckoutInitialization Initialization,
            string ProviderIdempotencyKey);
    }
}
