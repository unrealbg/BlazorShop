namespace BlazorShop.Tests.Infrastructure.PostgreSql
{
    using System.Data.Common;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.DTOs.Product.ProductVariant;
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

    using Npgsql;

    using Xunit;

    [Collection(PostgreSqlCollection.Name)]
    public sealed class InventoryReservationPostgreSqlTests
    {
        private readonly PostgreSqlFixture _database;

        public InventoryReservationPostgreSqlTests(PostgreSqlFixture database)
        {
            _database = database;
        }

        [Fact]
        public async Task ProductOnlyFinalUnit_AllowsExactlyOneConcurrentReservation()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 1);

            var results = await RunConcurrentlyAsync(
                () => ReserveAsync(CreateOrder(productId, null, 1)),
                () => ReserveAsync(CreateOrder(productId, null, 1)));

            Assert.Single(results, result => result.Success);
            Assert.Single(results, result => !result.Success);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(0, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(1, await assertionContext.InventoryReservations.CountAsync());
        }

        [Fact]
        public async Task VariantFinalUnit_AllowsExactlyOneConcurrentReservation()
        {
            await _database.ResetDatabaseAsync();
            var (productId, variantId) = await SeedVariantProductAsync(stock: 1);

            var results = await RunConcurrentlyAsync(
                () => ReserveAsync(CreateOrder(productId, variantId, 1)),
                () => ReserveAsync(CreateOrder(productId, variantId, 1)));

            Assert.Single(results, result => result.Success);
            Assert.Single(results, result => !result.Success);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(0, (await assertionContext.ProductVariants.FindAsync(variantId))!.Stock);
            Assert.Equal(0, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(1, await assertionContext.InventoryReservations.CountAsync());
        }

        [Fact]
        public async Task ImmediateConsumption_RecordsConsumedReservationWithoutSecondDeduction()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            var order = CreateOrder(productId, null, 1);
            await using var context = _database.CreateContext();

            var result = await new InventoryReservationService(context).CreateOrderWithInventoryAsync(
                order,
                InventoryReservationStatus.Consumed);

            Assert.True(result.Success);
            context.ChangeTracker.Clear();
            Assert.Equal(1, (await context.Products.FindAsync(productId))!.Quantity);
            var reservation = await context.InventoryReservations.SingleAsync();
            Assert.Equal(InventoryReservationStatus.Consumed, reservation.Status);
            Assert.NotNull(reservation.ConsumedOn);
        }

        [Fact]
        public async Task MultiLineFailure_RollsBackEveryInventoryTarget()
        {
            await _database.ResetDatabaseAsync();
            var availableProductId = await SeedProductAsync(quantity: 3, name: "Available");
            var unavailableProductId = await SeedProductAsync(quantity: 0, name: "Unavailable");
            var order = CreateOrder(
                (availableProductId, null, 2),
                (unavailableProductId, null, 1));

            var result = await ReserveAsync(order);

            Assert.False(result.Success);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(3, (await assertionContext.Products.FindAsync(availableProductId))!.Quantity);
            Assert.Equal(0, await assertionContext.InventoryReservations.CountAsync());
            Assert.Null(await assertionContext.Orders.FindAsync(order.Id));
        }

        [Fact]
        public async Task DuplicateInventoryTargets_AreAggregatedBeforeValidation()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 5);
            var order = CreateOrder(
                (productId, null, 3),
                (productId, null, 3));

            var result = await ReserveAsync(order);

            Assert.False(result.Success);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(5, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Empty(await assertionContext.InventoryReservations.ToListAsync());
        }

        [Fact]
        public async Task ConcurrentRelease_RestoresReservedStockExactlyOnce()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 5);
            var order = CreateOrder(productId, null, 2);
            Assert.True((await ReserveAsync(order)).Success);

            var results = await RunConcurrentlyAsync(
                () => TransitionAsync(order.Id, PaymentOrderStatus.Cancelled, InventoryReservationStatus.Released),
                () => TransitionAsync(order.Id, PaymentOrderStatus.Cancelled, InventoryReservationStatus.Released));

            Assert.All(results, result => Assert.True(result.Success));
            Assert.Single(results, result => result.Outcome == InventoryTransitionOutcome.Applied);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(5, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(
                InventoryReservationStatus.Released,
                (await assertionContext.InventoryReservations.SingleAsync()).Status);
        }

        [Fact]
        public async Task ConcurrentConsume_DoesNotDeductReservedStockAgain()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 5);
            var order = CreateOrder(productId, null, 2);
            Assert.True((await ReserveAsync(order)).Success);

            var results = await RunConcurrentlyAsync(
                () => TransitionAsync(order.Id, PaymentOrderStatus.Paid, InventoryReservationStatus.Consumed),
                () => TransitionAsync(order.Id, PaymentOrderStatus.Paid, InventoryReservationStatus.Consumed));

            Assert.All(results, result => Assert.True(result.Success));
            Assert.Single(results, result => result.Outcome == InventoryTransitionOutcome.Applied);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(3, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(
                InventoryReservationStatus.Consumed,
                (await assertionContext.InventoryReservations.SingleAsync()).Status);
        }

        [Fact]
        public async Task ConsumeThenLateRelease_DoesNotRestoreSoldStock()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 5);
            var order = CreateOrder(productId, null, 2);
            Assert.True((await ReserveAsync(order)).Success);
            Assert.True((await TransitionAsync(
                order.Id,
                PaymentOrderStatus.Paid,
                InventoryReservationStatus.Consumed)).Success);

            var release = await TransitionAsync(
                order.Id,
                PaymentOrderStatus.PaymentFailed,
                InventoryReservationStatus.Released);

            Assert.True(release.Success);
            Assert.Equal(InventoryTransitionOutcome.AlreadyApplied, release.Outcome);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(3, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(PaymentOrderStatus.Paid, (await assertionContext.Orders.FindAsync(order.Id))!.Status);
        }

        [Fact]
        public async Task PaymentInitializationFailure_ReleasesReservationAndRestoresStock()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            await using var context = _database.CreateContext();
            var paymentMethods = new Mock<IPaymentMethodService>();
            paymentMethods.Setup(service => service.GetPaymentMethodsAsync())
                .ReturnsAsync([new GetPaymentMethod { Id = PaymentMethodIds.CreditCard, Name = "Credit Card" }]);
            var payment = new Mock<IPaymentService>();
            payment.Setup(service => service.Pay(
                    It.IsAny<IReadOnlyCollection<ResolvedCartLine>>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Stripe unavailable",
                    FailureKind: PaymentInitializationFailureKind.Definitive));
            var users = new Mock<IAppUserManager>();
            users.Setup(manager => manager.GetUserByIdAsync("user-1"))
                .ReturnsAsync(new AppUser { Id = "user-1", Email = "user@example.com" });
            var inventory = new InventoryReservationService(context);
            var orchestrator = new CheckoutOrchestrator(
                new ProductReadRepository(context),
                paymentMethods.Object,
                payment.Object,
                users.Object,
                inventory,
                new CheckoutIdempotencyStore(
                    _database.CreateContextFactory(),
                    Options.Create(new CheckoutIdempotencyOptions())),
                new OrderRepository(context),
                Mock.Of<IEmailService>(),
                Options.Create(new BankTransferSettings()),
                Mock.Of<ILogger<CheckoutOrchestrator>>());

            var result = await orchestrator.CheckoutAsync(
                new Checkout
                {
                    PaymentMethodId = PaymentMethodIds.CreditCard,
                    Carts = [new CartLineRequest(productId, null, 1)],
                },
                "user-1",
                Guid.NewGuid());

            Assert.False(result.Success);
            context.ChangeTracker.Clear();
            Assert.Equal(2, (await context.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(PaymentOrderStatus.PaymentFailed, (await context.Orders.SingleAsync()).Status);
            Assert.Equal(
                InventoryReservationStatus.Released,
                (await context.InventoryReservations.SingleAsync()).Status);
        }

        [Fact]
        public async Task CashOnDeliveryCheckout_CommitsOrderAndConsumedReservationBeforeReturning()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            await using var context = _database.CreateContext();
            var orchestrator = CreateCheckoutOrchestrator(
                context,
                PaymentMethodIds.CashOnDelivery);

            var result = await orchestrator.CheckoutAsync(
                new Checkout
                {
                    PaymentMethodId = PaymentMethodIds.CashOnDelivery,
                    Carts = [new CartLineRequest(productId, null, 1)],
                },
                "customer-1",
                Guid.NewGuid());

            Assert.True(result.Success);
            Assert.NotNull(result.Payload);
            context.ChangeTracker.Clear();
            var order = await context.Orders
                .Include(item => item.Lines)
                .SingleAsync(item => item.Id == result.Payload!.OrderId);
            Assert.Equal("customer-1", order.UserId);
            Assert.Equal(result.Payload.OrderReference, order.Reference);
            Assert.Single(order.Lines);
            Assert.Equal(1, (await context.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(
                InventoryReservationStatus.Consumed,
                (await context.InventoryReservations.SingleAsync()).Status);
        }

        [Fact]
        public async Task BankTransferCheckout_EmailFailureLeavesOrderAndReservationCommitted()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            await using var context = _database.CreateContext();
            var email = new Mock<IEmailService>();
            email.Setup(service => service.SendEmailAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));
            var users = new Mock<IAppUserManager>();
            users.Setup(manager => manager.GetUserByIdAsync("customer-1"))
                .ReturnsAsync(new AppUser { Id = "customer-1", Email = "customer@example.com" });
            var orchestrator = CreateCheckoutOrchestrator(
                context,
                PaymentMethodIds.BankTransfer,
                email: email.Object,
                users: users.Object);

            var result = await orchestrator.CheckoutAsync(
                new Checkout
                {
                    PaymentMethodId = PaymentMethodIds.BankTransfer,
                    Carts = [new CartLineRequest(productId, null, 1)],
                },
                "customer-1",
                Guid.NewGuid());

            Assert.True(result.Success);
            Assert.NotNull(result.Payload?.BankTransfer);
            context.ChangeTracker.Clear();
            Assert.NotNull(await context.Orders.FindAsync(result.Payload!.OrderId));
            Assert.Equal(1, (await context.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(
                InventoryReservationStatus.Reserved,
                (await context.InventoryReservations.SingleAsync()).Status);
        }

        [Fact]
        public async Task StripeCheckout_InitializesProviderAfterPendingOrderAndReservationCommit()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            await using var context = _database.CreateContext();
            var payment = new Mock<IPaymentService>();
            payment.Setup(service => service.Pay(
                    It.IsAny<IReadOnlyCollection<ResolvedCartLine>>(),
                    It.IsAny<Guid>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (
                    IReadOnlyCollection<ResolvedCartLine> lines,
                    Guid orderId,
                    string reference,
                    string _,
                    CancellationToken _) =>
                {
                    await using var providerContext = _database.CreateContext();
                    var persistedOrder = await providerContext.Orders.FindAsync(orderId);
                    Assert.NotNull(persistedOrder);
                    Assert.Equal(PaymentOrderStatus.PendingPayment, persistedOrder.Status);
                    Assert.Equal(reference, persistedOrder.Reference);
                    Assert.Equal(
                        InventoryReservationStatus.Reserved,
                        (await providerContext.InventoryReservations.SingleAsync(
                            item => item.OrderId == orderId)).Status);
                    Assert.Equal(10m, Assert.Single(lines).UnitPrice);
                    return new PaymentInitializationResult(true, "https://checkout.stripe.test/session");
                });
            var orchestrator = CreateCheckoutOrchestrator(
                context,
                PaymentMethodIds.CreditCard,
                payment.Object);

            var result = await orchestrator.CheckoutAsync(
                new Checkout
                {
                    PaymentMethodId = PaymentMethodIds.CreditCard,
                    Carts = [new CartLineRequest(productId, null, 1)],
                },
                "customer-1",
                Guid.NewGuid());

            Assert.True(result.Success);
            Assert.Equal("https://checkout.stripe.test/session", result.Payload!.RedirectUrl);
            Assert.Equal(1, (await context.Products.FindAsync(productId))!.Quantity);
        }

        [Fact]
        public async Task StaleAdminVariantUpdate_ReturnsConflictWithoutResurrectingReservedStock()
        {
            await _database.ResetDatabaseAsync();
            var (productId, variantId) = await SeedVariantProductAsync(stock: 1);
            var order = CreateOrder(productId, variantId, 1);
            Assert.True((await ReserveAsync(order)).Success);
            await using var adminContext = _database.CreateContext();
            var admin = new BlazorShop.Infrastructure.Services.Admin.AdminInventoryService(
                adminContext,
                Mock.Of<BlazorShop.Application.Services.Contracts.Admin.IAdminAuditService>());

            var result = await admin.UpdateVariantStockAsync(
                variantId,
                new BlazorShop.Application.DTOs.Admin.Inventory.UpdateVariantStockDto
                {
                    Stock = 5,
                    ExpectedStock = 1,
                });

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.Conflict, result.ResponseType);
            adminContext.ChangeTracker.Clear();
            Assert.Equal(0, (await adminContext.ProductVariants.FindAsync(variantId))!.Stock);
        }

        [Fact]
        public async Task StaleGenericProductUpdate_DoesNotResurrectReservedStock()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 5);
            Product staleProduct;
            await using (var staleContext = _database.CreateContext())
            {
                staleProduct = (await staleContext.Products.AsNoTracking().SingleAsync(item => item.Id == productId));
            }

            Assert.True((await ReserveAsync(CreateOrder(productId, null, 1))).Success);
            await using var adminContext = _database.CreateContext();
            var service = new BlazorShop.Application.Services.ProductService(
                new ProductReadRepository(adminContext),
                new GenericRepository<Product>(adminContext),
                new ProductInventoryTopologyRepository(_database.CreateContextFactory()),
                AutoMapperTestFactory.CreateMapper());

            var result = await service.UpdateAsync(new UpdateProduct
            {
                Id = staleProduct.Id,
                Name = staleProduct.Name,
                Description = staleProduct.Description,
                Price = staleProduct.Price,
                Image = staleProduct.Image,
                Quantity = staleProduct.Quantity,
                CategoryId = staleProduct.CategoryId,
            });

            Assert.True(result.Success);
            adminContext.ChangeTracker.Clear();
            Assert.Equal(4, (await adminContext.Products.FindAsync(productId))!.Quantity);
        }

        [Fact]
        public async Task StaleGenericVariantUpdate_DoesNotResurrectReservedStock()
        {
            await _database.ResetDatabaseAsync();
            var (productId, variantId) = await SeedVariantProductAsync(stock: 5);
            ProductVariant staleVariant;
            await using (var staleContext = _database.CreateContext())
            {
                staleVariant = await staleContext.ProductVariants.AsNoTracking().SingleAsync(item => item.Id == variantId);
            }

            Assert.True((await ReserveAsync(CreateOrder(productId, variantId, 1))).Success);
            await using var adminContext = _database.CreateContext();
            var service = new BlazorShop.Application.Services.ProductVariantService(
                new GenericRepository<ProductVariant>(adminContext),
                new ProductInventoryTopologyRepository(_database.CreateContextFactory()),
                AutoMapperTestFactory.CreateMapper());

            var result = await service.UpdateAsync(new UpdateProductVariant
            {
                Id = staleVariant.Id,
                ProductId = staleVariant.ProductId,
                Sku = staleVariant.Sku,
                SizeScale = (int)staleVariant.SizeScale,
                SizeValue = staleVariant.SizeValue,
                Price = staleVariant.Price,
                Stock = staleVariant.Stock,
                Color = staleVariant.Color,
                IsDefault = staleVariant.IsDefault,
            });

            Assert.True(result.Success);
            adminContext.ChangeTracker.Clear();
            Assert.Equal(4, (await adminContext.ProductVariants.FindAsync(variantId))!.Stock);
        }

        [Fact]
        public async Task ReservedVariant_GenericUpdateCannotChangeItsProduct()
        {
            await _database.ResetDatabaseAsync();
            var (productAId, variantId) = await SeedVariantProductAsync(stock: 5, name: "Product A");
            var productBId = await SeedProductAsync(quantity: 0, name: "Product B");
            var order = CreateOrder(productAId, variantId, 1);
            Assert.True((await ReserveAsync(order)).Success);

            await using var adminContext = _database.CreateContext();
            var existingVariant = await adminContext.ProductVariants
                .AsNoTracking()
                .SingleAsync(item => item.Id == variantId);
            var service = new BlazorShop.Application.Services.ProductVariantService(
                new GenericRepository<ProductVariant>(adminContext),
                new ProductInventoryTopologyRepository(_database.CreateContextFactory()),
                AutoMapperTestFactory.CreateMapper());

            var update = await service.UpdateAsync(new UpdateProductVariant
            {
                Id = existingVariant.Id,
                ProductId = productBId,
                Sku = existingVariant.Sku,
                SizeScale = (int)existingVariant.SizeScale,
                SizeValue = existingVariant.SizeValue,
                Price = existingVariant.Price,
                Stock = 99,
                Color = existingVariant.Color,
                IsDefault = existingVariant.IsDefault,
            });

            Assert.False(update.Success);
            Assert.Equal("A product variant cannot be moved to another product", update.Message);
            adminContext.ChangeTracker.Clear();
            var reservedVariant = await adminContext.ProductVariants.FindAsync(variantId);
            Assert.NotNull(reservedVariant);
            Assert.Equal(productAId, reservedVariant.ProductId);
            Assert.Equal(4, reservedVariant.Stock);

            Assert.True((await TransitionAsync(
                order.Id,
                PaymentOrderStatus.Cancelled,
                InventoryReservationStatus.Released)).Success);
            Assert.True((await TransitionAsync(
                order.Id,
                PaymentOrderStatus.Cancelled,
                InventoryReservationStatus.Released)).Success);

            adminContext.ChangeTracker.Clear();
            var releasedVariant = await adminContext.ProductVariants.FindAsync(variantId);
            Assert.NotNull(releasedVariant);
            Assert.Equal(productAId, releasedVariant.ProductId);
            Assert.Equal(5, releasedVariant.Stock);
        }

        [Fact]
        public async Task DeleteLastVariant_LeavesProductQuantityNonSellable()
        {
            await _database.ResetDatabaseAsync();
            var (productId, variantId) = await SeedVariantProductAsync(stock: 2);
            await using (var legacyContext = _database.CreateContext())
            {
                await legacyContext.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE \"Products\" SET \"Quantity\" = {99} WHERE \"Id\" = {productId}");
            }

            var repository = new ProductInventoryTopologyRepository(_database.CreateContextFactory());
            var result = await repository.DeleteVariantAsync(variantId);

            Assert.True(result.Success);
            await using var assertionContext = _database.CreateContext();
            Assert.Null(await assertionContext.ProductVariants.FindAsync(variantId));
            Assert.Equal(0, (await assertionContext.Products.FindAsync(productId))!.Quantity);
        }

        [Fact]
        public async Task ReservedVariant_CannotBeDeleted()
        {
            await _database.ResetDatabaseAsync();
            var (productId, variantId) = await SeedVariantProductAsync(stock: 2);
            Assert.True((await ReserveAsync(CreateOrder(productId, variantId, 1))).Success);

            var result = await new ProductInventoryTopologyRepository(_database.CreateContextFactory())
                .DeleteVariantAsync(variantId);

            Assert.False(result.Success);
            await using var assertionContext = _database.CreateContext();
            Assert.NotNull(await assertionContext.ProductVariants.FindAsync(variantId));
            Assert.Equal(1, (await assertionContext.ProductVariants.FindAsync(variantId))!.Stock);
        }

        [Fact]
        public async Task ReservedProduct_CannotBeDeleted()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 2);
            Assert.True((await ReserveAsync(CreateOrder(productId, null, 1))).Success);

            var result = await new ProductInventoryTopologyRepository(_database.CreateContextFactory())
                .DeleteProductAsync(productId);

            Assert.False(result.Success);
            await using var assertionContext = _database.CreateContext();
            Assert.NotNull(await assertionContext.Products.FindAsync(productId));
            Assert.Equal(1, (await assertionContext.Products.FindAsync(productId))!.Quantity);
        }

        [Fact]
        public async Task AddFirstVariant_WithActiveProductReservation_IsRejected()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 3);
            Assert.True((await ReserveAsync(CreateOrder(productId, null, 1))).Success);
            var variant = new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                SizeScale = SizeScale.ClothingAlpha,
                SizeValue = "M",
                Stock = 8,
            };

            var result = await new ProductInventoryTopologyRepository(_database.CreateContextFactory())
                .AddVariantAsync(variant);

            Assert.False(result.Success);
            await using var assertionContext = _database.CreateContext();
            Assert.False(await assertionContext.ProductVariants.AnyAsync(item => item.ProductId == productId));
            Assert.Equal(2, (await assertionContext.Products.FindAsync(productId))!.Quantity);
        }

        [Fact]
        public async Task AddFirstVariant_NormalizesProductQuantityAndIgnoresRequestedStock()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 99);
            var variant = new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                SizeScale = SizeScale.ClothingAlpha,
                SizeValue = "L",
                Stock = 8,
            };

            var result = await new ProductInventoryTopologyRepository(_database.CreateContextFactory())
                .AddVariantAsync(variant);

            Assert.True(result.Success);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(0, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(0, (await assertionContext.ProductVariants.FindAsync(variant.Id))!.Stock);
        }

        [Fact]
        public async Task MissingReservedTarget_StillReleasesReservationAndTerminalizesOrder()
        {
            await _database.ResetDatabaseAsync();
            var (productId, variantId) = await SeedVariantProductAsync(stock: 2);
            var order = CreateOrder(productId, variantId, 1);
            Assert.True((await ReserveAsync(order)).Success);
            await using (var corruptionContext = _database.CreateContext())
            {
                await corruptionContext.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM \"ProductVariants\" WHERE \"Id\" = {variantId}");
            }

            var result = await TransitionAsync(
                order.Id,
                PaymentOrderStatus.PaymentFailed,
                InventoryReservationStatus.Released);

            Assert.True(result.Success);
            Assert.Equal(InventoryTransitionOutcome.Applied, result.Outcome);
            Assert.NotNull(result.ErrorMessage);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(PaymentOrderStatus.PaymentFailed, (await assertionContext.Orders.FindAsync(order.Id))!.Status);
            Assert.Equal(
                InventoryReservationStatus.Released,
                (await assertionContext.InventoryReservations.SingleAsync()).Status);
            Assert.Equal(0, (await assertionContext.Products.FindAsync(productId))!.Quantity);
        }

        [Fact]
        public async Task AmbiguousReleaseCommit_IsVerifiedWithoutDoubleRestoringStock()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 5);
            var order = CreateOrder(productId, null, 2);
            Assert.True((await ReserveAsync(order)).Success);
            var interceptor = new ThrowOnceAfterCommitInterceptor();
            var service = new InventoryReservationService(_database.CreateContextFactory(interceptor));

            var result = await service.TransitionOrderAsync(
                order.Id,
                PaymentOrderStatus.Cancelled,
                InventoryReservationStatus.Released);

            Assert.Equal(InventoryTransitionOutcome.Applied, result.Outcome);
            Assert.True(interceptor.FaultInjected);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(5, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(
                InventoryReservationStatus.Released,
                (await assertionContext.InventoryReservations.SingleAsync()).Status);
        }

        [Fact]
        public async Task AmbiguousCreateCommit_IsVerifiedWithoutDoubleDeductingInventory()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 5);
            var order = CreateOrder(productId, null, 2);
            var interceptor = new ThrowOnceAfterCommitInterceptor();
            var service = new InventoryReservationService(_database.CreateContextFactory(interceptor));

            var result = await service.CreateOrderWithInventoryAsync(
                order,
                InventoryReservationStatus.Reserved);

            Assert.True(result.Success);
            Assert.True(interceptor.FaultInjected);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(3, (await assertionContext.Products.FindAsync(productId))!.Quantity);
            Assert.Equal(1, await assertionContext.Orders.CountAsync(item => item.Id == order.Id));
            Assert.Equal(1, await assertionContext.InventoryReservations.CountAsync(item => item.OrderId == order.Id));
        }

        [Fact]
        public async Task DatabaseConstraints_RejectNegativeProductAndVariantInventory()
        {
            await _database.ResetDatabaseAsync();
            var productId = await SeedProductAsync(quantity: 1);
            var (_, variantId) = await SeedVariantProductAsync(stock: 1, name: "Variant product");

            await using (var productContext = _database.CreateContext())
            {
                var exception = await Assert.ThrowsAsync<PostgresException>(() =>
                    productContext.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"Products\" SET \"Quantity\" = {-1} WHERE \"Id\" = {productId}"));
                Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
            }

            await using (var variantContext = _database.CreateContext())
            {
                var exception = await Assert.ThrowsAsync<PostgresException>(() =>
                    variantContext.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE \"ProductVariants\" SET \"Stock\" = {-1} WHERE \"Id\" = {variantId}"));
                Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
            }
        }

        private async Task<InventoryReservationResult> ReserveAsync(Order order)
        {
            await using var context = _database.CreateContext();
            return await new InventoryReservationService(context).CreateOrderWithInventoryAsync(
                order,
                InventoryReservationStatus.Reserved);
        }

        private async Task<InventoryTransitionResult> TransitionAsync(
            Guid orderId,
            string orderStatus,
            InventoryReservationStatus reservationStatus)
        {
            await using var context = _database.CreateContext();
            return await new InventoryReservationService(context).TransitionOrderAsync(
                orderId,
                orderStatus,
                reservationStatus);
        }

        private static async Task<T[]> RunConcurrentlyAsync<T>(Func<Task<T>> first, Func<Task<T>> second)
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

        private CheckoutOrchestrator CreateCheckoutOrchestrator(
            BlazorShop.Infrastructure.Data.AppDbContext context,
            Guid paymentMethodId,
            IPaymentService? payment = null,
            IEmailService? email = null,
            IAppUserManager? users = null)
        {
            var paymentMethods = new Mock<IPaymentMethodService>();
            paymentMethods.Setup(service => service.GetPaymentMethodsAsync())
                .ReturnsAsync([new GetPaymentMethod { Id = paymentMethodId, Name = "Available" }]);
            if (users is null)
            {
                var userManager = new Mock<IAppUserManager>();
                userManager.Setup(manager => manager.GetUserByIdAsync("customer-1"))
                    .ReturnsAsync(new AppUser { Id = "customer-1", Email = "customer@example.com" });
                users = userManager.Object;
            }

            return new CheckoutOrchestrator(
                new ProductReadRepository(context),
                paymentMethods.Object,
                payment ?? Mock.Of<IPaymentService>(),
                users,
                new InventoryReservationService(context),
                new CheckoutIdempotencyStore(
                    _database.CreateContextFactory(),
                    Options.Create(new CheckoutIdempotencyOptions())),
                new OrderRepository(context),
                email ?? Mock.Of<IEmailService>(),
                Options.Create(new BankTransferSettings
                {
                    Iban = "BG00TEST",
                    Beneficiary = "Blazor Shop",
                    BankName = "Test Bank",
                }),
                Mock.Of<ILogger<CheckoutOrchestrator>>());
        }

        private async Task<Guid> SeedProductAsync(int quantity, string name = "Product")
        {
            await using var context = _database.CreateContext();
            var category = await GetOrCreateCategoryAsync(context);
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"{name}-{Guid.NewGuid():N}",
                Price = 10m,
                Quantity = quantity,
                CategoryId = category.Id,
            };
            context.Products.Add(product);
            await context.SaveChangesAsync();
            return product.Id;
        }

        private async Task<(Guid ProductId, Guid VariantId)> SeedVariantProductAsync(
            int stock,
            string name = "Variant")
        {
            await using var context = _database.CreateContext();
            var category = await GetOrCreateCategoryAsync(context);
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = $"{name}-{Guid.NewGuid():N}",
                Price = 10m,
                Quantity = 0,
                CategoryId = category.Id,
            };
            var variant = new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Price = 12m,
                Stock = stock,
                SizeScale = SizeScale.ShoesEU,
                SizeValue = Guid.NewGuid().ToString("N")[..8],
            };
            context.AddRange(product, variant);
            await context.SaveChangesAsync();
            return (product.Id, variant.Id);
        }

        private static async Task<Category> GetOrCreateCategoryAsync(BlazorShop.Infrastructure.Data.AppDbContext context)
        {
            var category = await context.Categories.FirstOrDefaultAsync();
            if (category is not null)
            {
                return category;
            }

            category = new Category { Id = Guid.NewGuid(), Name = "Inventory" };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            return category;
        }

        private static Order CreateOrder(Guid productId, Guid? variantId, int quantity)
        {
            return CreateOrder((productId, variantId, quantity));
        }

        private static Order CreateOrder(params (Guid ProductId, Guid? VariantId, int Quantity)[] lines)
        {
            return new Order
            {
                Id = Guid.NewGuid(),
                UserId = "inventory-test",
                Status = PaymentOrderStatus.PendingPayment,
                Reference = $"INV-{Guid.NewGuid():N}",
                TotalAmount = lines.Sum(line => line.Quantity * 10m),
                Lines = lines.Select(line => new OrderLine
                {
                    ProductId = line.ProductId,
                    ProductVariantId = line.VariantId,
                    ProductNameSnapshot = "Inventory product",
                    Quantity = line.Quantity,
                    UnitPrice = 10m,
                    LineTotal = line.Quantity * 10m,
                }).ToList(),
            };
        }

        private sealed class ThrowOnceAfterCommitInterceptor : DbTransactionInterceptor
        {
            private int _faultsRemaining = 1;

            public bool FaultInjected => Volatile.Read(ref _faultsRemaining) == 0;

            public override Task TransactionCommittedAsync(
                DbTransaction transaction,
                TransactionEndEventData eventData,
                CancellationToken cancellationToken = default)
            {
                if (Interlocked.Exchange(ref _faultsRemaining, 0) == 1)
                {
                    throw new TimeoutException("Injected ambiguous commit failure.");
                }

                return Task.CompletedTask;
            }
        }
    }
}
