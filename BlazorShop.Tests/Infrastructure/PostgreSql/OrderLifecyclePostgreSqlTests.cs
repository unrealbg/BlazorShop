namespace BlazorShop.Tests.Infrastructure.PostgreSql
{
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;
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
        public async Task LegacyMigration_UsesPaymentEvidenceAndKeepsUnknownPaidClaimConservative()
        {
            await _database.ResetDatabaseAsync();
            await using var context = _database.CreateContext();
            await context.Database.MigrateAsync(PreviousMigration);

            var paidOrderId = Guid.NewGuid();
            var unknownOrderId = Guid.NewGuid();
            await context.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO "Orders" ("Id", "UserId", "Status", "Reference", "TotalAmount", "Currency", "CreatedOn", "ShippingStatus")
                VALUES
                    ({{paidOrderId}}, 'legacy-paid', 'Paid', 'LEGACY-PAID', 20, 'EUR', CURRENT_TIMESTAMP, 'Delivered'),
                    ({{unknownOrderId}}, 'legacy-unknown', 'Paid', 'LEGACY-UNKNOWN', 15, 'EUR', CURRENT_TIMESTAMP, 'PendingShipment');

                INSERT INTO "PaymentTransactions"
                    ("Id", "OrderId", "Provider", "ProviderSessionId", "ExpectedAmountMinor", "Currency", "Status", "CreatedOn", "UpdatedOn", "PaidOn")
                VALUES
                    ({{Guid.NewGuid()}}, {{paidOrderId}}, 'Stripe', 'cs_legacy_paid', 2000, 'EUR', 'Paid', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP);
                """);

            await context.Database.MigrateAsync();
            context.ChangeTracker.Clear();

            var paid = await context.Orders.SingleAsync(order => order.Id == paidOrderId);
            Assert.Equal(OrderStatus.Completed, paid.OrderStatus);
            Assert.Equal(OrderPaymentStatus.Paid, paid.PaymentStatus);
            Assert.Equal(OrderPaymentMethod.Stripe, paid.PaymentMethod);
            Assert.Equal(FulfillmentStatus.Delivered, paid.FulfillmentStatus);
            Assert.Equal("Paid", paid.LegacyStatus);
            Assert.Equal("Delivered", paid.LegacyShippingStatus);
            Assert.Equal(20m, paid.SubtotalAmount);

            var unknown = await context.Orders.SingleAsync(order => order.Id == unknownOrderId);
            Assert.Equal(OrderStatus.Pending, unknown.OrderStatus);
            Assert.Equal(OrderPaymentStatus.Pending, unknown.PaymentStatus);
            Assert.Equal(OrderPaymentMethod.Unknown, unknown.PaymentMethod);
            Assert.Equal(FulfillmentStatus.NotStarted, unknown.FulfillmentStatus);
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
    }
}
