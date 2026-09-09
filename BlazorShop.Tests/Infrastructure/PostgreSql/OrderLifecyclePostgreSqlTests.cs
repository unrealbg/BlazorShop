namespace BlazorShop.Tests.Infrastructure.PostgreSql
{
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Services;

    using Microsoft.EntityFrameworkCore;

    using Moq;

    using Xunit;

    [Collection(PostgreSqlCollection.Name)]
    public sealed class OrderLifecyclePostgreSqlTests
    {
        private const string PreviousMigration = "20260821084312_AddPaymentReconciliation";
        private readonly PostgreSqlFixture _database;

        public OrderLifecyclePostgreSqlTests(PostgreSqlFixture database)
        {
            _database = database;
        }

        [Fact]
        public async Task LegacyMigration_CaseInsensitiveDeliveredRemainsCompletedAndCannotShipAgain()
        {
            await _database.ResetDatabaseAsync();
            await using var context = _database.CreateContext();
            await context.Database.MigrateAsync(PreviousMigration);
            var evidence = await InsertLegacyPaidOrderAsync(context, "dElIvErEd", "LEGACY-DELIVERED");

            await context.Database.MigrateAsync();
            context.ChangeTracker.Clear();
            var inventory = await AddInventoryEvidenceAsync(context, evidence.OrderId);

            var migrated = await context.Orders.AsNoTracking().SingleAsync(order => order.Id == evidence.OrderId);
            Assert.Equal(OrderStatus.Completed, migrated.OrderStatus);
            Assert.Equal(OrderPaymentStatus.Paid, migrated.PaymentStatus);
            Assert.Equal(OrderPaymentMethod.Stripe, migrated.PaymentMethod);
            Assert.Equal(FulfillmentStatus.Delivered, migrated.FulfillmentStatus);
            Assert.Equal("Paid", migrated.LegacyStatus);
            Assert.Equal("dElIvErEd", migrated.LegacyShippingStatus);

            var service = new OrderTrackingService(context, Mock.Of<IEmailService>());
            var result = await service.TransitionFulfillmentAsync(evidence.OrderId, "Shipped");

            Assert.Equal(OrderTrackingTransitionOutcome.Conflict, result.Outcome);
            await AssertCommerceEvidenceUnchangedAsync(context, evidence, inventory);
        }

        [Fact]
        public async Task LegacyMigration_CaseInsensitivePendingShipmentPreservesPaymentEvidenceRules()
        {
            await _database.ResetDatabaseAsync();
            await using var context = _database.CreateContext();
            await context.Database.MigrateAsync(PreviousMigration);
            var evidence = await InsertLegacyPaidOrderAsync(context, "pendingSHIPMENT", "LEGACY-PENDING-PAID");
            var unprovenOrderId = Guid.NewGuid();
            await context.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO "Orders" ("Id", "UserId", "Status", "Reference", "TotalAmount", "Currency", "CreatedOn", "ShippingStatus")
                VALUES ({{unprovenOrderId}}, 'legacy-unproven', 'Paid', 'LEGACY-UNPROVEN', 15, 'EUR', CURRENT_TIMESTAMP, 'PENDINGshipment');
                """);

            await context.Database.MigrateAsync();
            context.ChangeTracker.Clear();
            var inventory = await AddInventoryEvidenceAsync(context, evidence.OrderId);

            var paid = await context.Orders.AsNoTracking().SingleAsync(order => order.Id == evidence.OrderId);
            Assert.Equal(OrderStatus.Confirmed, paid.OrderStatus);
            Assert.Equal(OrderPaymentStatus.Paid, paid.PaymentStatus);
            Assert.Equal(FulfillmentStatus.NotStarted, paid.FulfillmentStatus);
            Assert.Equal("pendingSHIPMENT", paid.LegacyShippingStatus);

            var unproven = await context.Orders.AsNoTracking().SingleAsync(order => order.Id == unprovenOrderId);
            Assert.Equal(OrderStatus.Pending, unproven.OrderStatus);
            Assert.Equal(OrderPaymentStatus.Pending, unproven.PaymentStatus);
            Assert.Equal(OrderPaymentMethod.Unknown, unproven.PaymentMethod);
            Assert.Equal(FulfillmentStatus.NotStarted, unproven.FulfillmentStatus);
            Assert.Equal("PENDINGshipment", unproven.LegacyShippingStatus);

            var shippedOn = new DateTime(2024, 1, 2, 10, 0, 0, DateTimeKind.Utc);
            var service = new OrderTrackingService(context, Mock.Of<IEmailService>());
            var result = await service.TransitionFulfillmentAsync(evidence.OrderId, "Shipped", shippedOn);

            Assert.Equal(OrderTrackingTransitionOutcome.Applied, result.Outcome);
            await AssertCommerceEvidenceUnchangedAsync(context, evidence, inventory);
            context.ChangeTracker.Clear();
            var shipped = await context.Orders.AsNoTracking().SingleAsync(order => order.Id == evidence.OrderId);
            Assert.Equal(FulfillmentStatus.Shipped, shipped.FulfillmentStatus);
            Assert.Equal(shippedOn, shipped.ShippedOn);
        }

        [Theory]
        [InlineData("sHiPpEd", FulfillmentStatus.Shipped)]
        [InlineData("INTRANSIT", FulfillmentStatus.InTransit)]
        [InlineData("outForDelivery", FulfillmentStatus.OutForDelivery)]
        public async Task LegacyMigration_InProgressWithoutShippedOnCanCompleteWithoutInventingTimestamp(
            string legacyStatus,
            FulfillmentStatus expectedStatus)
        {
            await _database.ResetDatabaseAsync();
            await using var context = _database.CreateContext();
            await context.Database.MigrateAsync(PreviousMigration);
            var evidence = await InsertLegacyPaidOrderAsync(context, legacyStatus, $"LEGACY-{expectedStatus}");

            await context.Database.MigrateAsync();
            context.ChangeTracker.Clear();
            var inventory = await AddInventoryEvidenceAsync(context, evidence.OrderId);

            var migrated = await context.Orders.AsNoTracking().SingleAsync(order => order.Id == evidence.OrderId);
            Assert.Equal(expectedStatus, migrated.FulfillmentStatus);
            Assert.Equal(legacyStatus, migrated.LegacyShippingStatus);
            Assert.Null(migrated.ShippedOn);

            var service = new OrderTrackingService(context, Mock.Of<IEmailService>());
            var deliveredOn = new DateTime(2024, 1, 3, 10, 0, 0, DateTimeKind.Utc);
            var result = await service.TransitionFulfillmentAsync(
                evidence.OrderId,
                "Delivered",
                deliveredOn: deliveredOn);

            Assert.Equal(OrderTrackingTransitionOutcome.Applied, result.Outcome);
            context.ChangeTracker.Clear();
            var delivered = await context.Orders.AsNoTracking().SingleAsync(order => order.Id == evidence.OrderId);
            Assert.Equal(OrderStatus.Completed, delivered.OrderStatus);
            Assert.Equal(OrderPaymentStatus.Paid, delivered.PaymentStatus);
            Assert.Equal(FulfillmentStatus.Delivered, delivered.FulfillmentStatus);
            Assert.Null(delivered.ShippedOn);
            Assert.Equal(deliveredOn, delivered.DeliveredOn);
            Assert.Equal(legacyStatus, delivered.LegacyShippingStatus);

            var repeated = await service.TransitionFulfillmentAsync(
                evidence.OrderId,
                "Delivered",
                deliveredOn: deliveredOn.AddMinutes(1));

            Assert.Equal(OrderTrackingTransitionOutcome.AlreadyApplied, repeated.Outcome);
            await AssertCommerceEvidenceUnchangedAsync(context, evidence, inventory);
            context.ChangeTracker.Clear();
            var repeatedOrder = await context.Orders.AsNoTracking().SingleAsync(order => order.Id == evidence.OrderId);
            Assert.Equal(deliveredOn, repeatedOrder.DeliveredOn);
            Assert.Null(repeatedOrder.ShippedOn);
        }

        [Fact]
        public async Task LegacyMigration_UnknownPaidShippingStateRequiresReviewAndCannotStartFulfillment()
        {
            await _database.ResetDatabaseAsync();
            await using var context = _database.CreateContext();
            await context.Database.MigrateAsync(PreviousMigration);
            var evidence = await InsertLegacyPaidOrderAsync(context, "AwaitingCarrierAudit", "LEGACY-REVIEW");

            await context.Database.MigrateAsync();
            context.ChangeTracker.Clear();
            var inventory = await AddInventoryEvidenceAsync(context, evidence.OrderId);

            var migrated = await context.Orders.AsNoTracking().SingleAsync(order => order.Id == evidence.OrderId);
            Assert.Equal(OrderStatus.Confirmed, migrated.OrderStatus);
            Assert.Equal(OrderPaymentStatus.Paid, migrated.PaymentStatus);
            Assert.Equal(FulfillmentStatus.ReviewRequired, migrated.FulfillmentStatus);
            Assert.Equal("AwaitingCarrierAudit", migrated.LegacyShippingStatus);

            var service = new OrderTrackingService(context, Mock.Of<IEmailService>());
            var result = await service.TransitionFulfillmentAsync(evidence.OrderId, "Shipped");

            Assert.Equal(OrderTrackingTransitionOutcome.Conflict, result.Outcome);
            Assert.Contains("manual review", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            await AssertCommerceEvidenceUnchangedAsync(context, evidence, inventory);
        }

        [Fact]
        public async Task NewOrdersStillRejectMissingAndReverseShipmentTimestamps()
        {
            await _database.ResetDatabaseAsync();
            var missingTimestampOrderId = Guid.NewGuid();
            var reverseTimestampOrderId = Guid.NewGuid();
            var shippedOn = DateTime.UtcNow.AddHours(-1);
            await using var context = _database.CreateContext();
            context.Orders.AddRange(
                CreateCurrentOrder(missingTimestampOrderId, "NEW-MISSING-SHIPPED", null),
                CreateCurrentOrder(reverseTimestampOrderId, "NEW-REVERSE-TIMESTAMP", shippedOn));
            await context.SaveChangesAsync();
            var service = new OrderTrackingService(context, Mock.Of<IEmailService>());

            var missing = await service.TransitionFulfillmentAsync(missingTimestampOrderId, "Delivered");
            var reverse = await service.TransitionFulfillmentAsync(
                reverseTimestampOrderId,
                "Delivered",
                deliveredOn: shippedOn.AddMinutes(-1));

            Assert.Equal(OrderTrackingTransitionOutcome.ValidationError, missing.Outcome);
            Assert.Equal(OrderTrackingTransitionOutcome.ValidationError, reverse.Outcome);
            context.ChangeTracker.Clear();
            var orders = await context.Orders.AsNoTracking()
                .Where(order => order.Id == missingTimestampOrderId || order.Id == reverseTimestampOrderId)
                .ToListAsync();
            Assert.All(orders, order => Assert.Equal(FulfillmentStatus.InTransit, order.FulfillmentStatus));
            Assert.All(orders, order => Assert.Equal(OrderStatus.Confirmed, order.OrderStatus));
            Assert.All(orders, order => Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus));
        }

        [Fact]
        public async Task StaleAdminCannotRestoreEarlierFulfillmentState()
        {
            await _database.ResetDatabaseAsync();
            var orderId = Guid.NewGuid();
            await using (var seedContext = _database.CreateContext())
            {
                seedContext.Orders.Add(new Order
                {
                    Id = orderId,
                    UserId = "customer",
                    Reference = "STALE-FULFILLMENT",
                    OrderStatus = OrderStatus.Confirmed,
                    PaymentStatus = OrderPaymentStatus.Paid,
                    PaymentMethod = OrderPaymentMethod.Stripe,
                    FulfillmentStatus = FulfillmentStatus.Shipped,
                    ShippedOn = DateTime.UtcNow.AddHours(-1),
                    Currency = "EUR",
                });
                await seedContext.SaveChangesAsync();
            }

            await using var staleContext = _database.CreateContext();
            _ = await staleContext.Orders.SingleAsync(order => order.Id == orderId);
            await using (var currentContext = _database.CreateContext())
            {
                var currentService = new OrderTrackingService(currentContext, Mock.Of<IEmailService>());
                Assert.Equal(
                    OrderTrackingTransitionOutcome.Applied,
                    (await currentService.TransitionFulfillmentAsync(orderId, "InTransit")).Outcome);
            }

            var staleService = new OrderTrackingService(staleContext, Mock.Of<IEmailService>());
            var staleResult = await staleService.TransitionFulfillmentAsync(orderId, "Shipped");

            Assert.Equal(OrderTrackingTransitionOutcome.Conflict, staleResult.Outcome);
            await using var assertionContext = _database.CreateContext();
            Assert.Equal(
                FulfillmentStatus.InTransit,
                (await assertionContext.Orders.SingleAsync(order => order.Id == orderId)).FulfillmentStatus);
        }

        private static Order CreateCurrentOrder(Guid id, string reference, DateTime? shippedOn) => new()
        {
            Id = id,
            UserId = "customer",
            Reference = reference,
            OrderStatus = OrderStatus.Confirmed,
            PaymentStatus = OrderPaymentStatus.Paid,
            PaymentMethod = OrderPaymentMethod.Stripe,
            FulfillmentStatus = FulfillmentStatus.InTransit,
            ShippedOn = shippedOn,
            Currency = "EUR",
        };

        private static async Task<LegacyCommerceEvidence> InsertLegacyPaidOrderAsync(
            AppDbContext context,
            string shippingStatus,
            string reference)
        {
            var orderId = Guid.NewGuid();
            var transactionId = Guid.NewGuid();
            var sessionId = $"cs_{orderId:N}";
            await context.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO "Orders" ("Id", "UserId", "Status", "Reference", "TotalAmount", "Currency", "CreatedOn", "ShippingStatus")
                VALUES ({{orderId}}, 'legacy-paid', 'Paid', {{reference}}, 20, 'EUR', CURRENT_TIMESTAMP, {{shippingStatus}});

                INSERT INTO "PaymentTransactions"
                    ("Id", "OrderId", "Provider", "ProviderSessionId", "ExpectedAmountMinor", "Currency", "Status", "CreatedOn", "UpdatedOn", "PaidOn")
                VALUES
                    ({{transactionId}}, {{orderId}}, 'Stripe', {{sessionId}}, 2000, 'EUR', 'Paid', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
                """);

            return new(orderId, transactionId, sessionId);
        }

        private static async Task<InventoryEvidence> AddInventoryEvidenceAsync(AppDbContext context, Guid orderId)
        {
            var category = new Category { Id = Guid.NewGuid(), Name = "Lifecycle migration" };
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = "Lifecycle product",
                CategoryId = category.Id,
                Category = category,
                Price = 20m,
                Quantity = 7,
            };
            var consumedOn = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
            var reservation = new InventoryReservation
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                ProductId = product.Id,
                Quantity = 1,
                Status = InventoryReservationStatus.Consumed,
                ConsumedOn = consumedOn,
            };
            context.AddRange(product, reservation);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return new(product.Id, product.Quantity, reservation.Id, reservation.Status, consumedOn);
        }

        private static async Task AssertCommerceEvidenceUnchangedAsync(
            AppDbContext context,
            LegacyCommerceEvidence evidence,
            InventoryEvidence inventory)
        {
            context.ChangeTracker.Clear();
            var transaction = await context.PaymentTransactions.AsNoTracking()
                .SingleAsync(item => item.Id == evidence.TransactionId);
            Assert.Equal(PaymentTransactionStatus.Paid, transaction.Status);
            Assert.Equal("Stripe", transaction.Provider);
            Assert.Equal(evidence.ProviderSessionId, transaction.ProviderSessionId);
            Assert.Equal(2000, transaction.ExpectedAmountMinor);
            Assert.Equal("EUR", transaction.Currency);

            var product = await context.Products.AsNoTracking().SingleAsync(item => item.Id == inventory.ProductId);
            Assert.Equal(inventory.ProductQuantity, product.Quantity);
            var reservation = await context.InventoryReservations.AsNoTracking()
                .SingleAsync(item => item.Id == inventory.ReservationId);
            Assert.Equal(inventory.ReservationStatus, reservation.Status);
            Assert.Equal(inventory.ConsumedOn, reservation.ConsumedOn);
            Assert.Null(reservation.ReleasedOn);

            var order = await context.Orders.AsNoTracking().SingleAsync(item => item.Id == evidence.OrderId);
            Assert.Equal(20m, order.TotalAmount);
            Assert.Equal(20m, order.SubtotalAmount);
            Assert.Equal("EUR", order.Currency);
        }

        private sealed record LegacyCommerceEvidence(Guid OrderId, Guid TransactionId, string ProviderSessionId);

        private sealed record InventoryEvidence(
            Guid ProductId,
            int ProductQuantity,
            Guid ReservationId,
            InventoryReservationStatus ReservationStatus,
            DateTime ConsumedOn);
    }
}
