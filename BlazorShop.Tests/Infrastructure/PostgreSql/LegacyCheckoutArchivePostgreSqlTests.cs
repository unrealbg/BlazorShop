namespace BlazorShop.Tests.Infrastructure.PostgreSql
{
    using System.Data;

    using BlazorShop.API;
    using BlazorShop.Application.Services;
    using BlazorShop.Application.Services.Payment;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Contracts.Newsletters;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Repositories.Payment;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Migrations;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using Xunit;

    [Collection(PostgreSqlCollection.Name)]
    public sealed class LegacyCheckoutArchivePostgreSqlTests
    {
        private const string InitialMigration = "20260412183234_InitialCreate";
        private const string PreviousMigration = "20260908215523_SeparateOrderPaymentAndFulfillmentStatuses";
        private const string CurrentMigration = "20260909075202_ArchiveLegacyCheckoutOrderItems";
        private const string ArchiveTable = "LegacyCheckoutOrderItemsArchive";
        private readonly PostgreSqlFixture _database;

        public LegacyCheckoutArchivePostgreSqlTests(PostgreSqlFixture database)
        {
            _database = database;
        }

        [Fact]
        public async Task FreshDatabaseAndNormalBootstrapApplyFullChainWithoutActiveLegacyModel()
        {
            await using (var context = _database.CreateContext())
            {
                await ResetSchemaAsync(context);
            }

            using var provider = CreateBootstrapProvider();
            await DatabaseMigrationBootstrapper.MigrateAsync(provider);
            await DatabaseMigrationBootstrapper.MigrateAsync(provider);

            await using var assertionContext = _database.CreateContext();
            Assert.Contains(CurrentMigration, await assertionContext.Database.GetAppliedMigrationsAsync());
            Assert.False(await TableExistsAsync(assertionContext, "CheckoutOrderItems"));
            Assert.True(await TableExistsAsync(assertionContext, ArchiveTable));
            Assert.DoesNotContain(
                assertionContext.Model.GetEntityTypes(),
                entity => entity.ClrType.FullName == "BlazorShop.Domain.Entities.Payment.OrderItem");
        }

        [Fact]
        public async Task HistoricalInitialSchemaWithoutMigrationHistoryStillBaselinesAndArchivesLosslessly()
        {
            var legacyRow = new ArchivedCheckoutRow(
                Guid.NewGuid(),
                Guid.NewGuid(),
                3,
                "baseline-user",
                new DateTime(2024, 2, 3, 4, 5, 6, DateTimeKind.Utc));
            await using (var context = _database.CreateContext())
            {
                await ResetSchemaAsync(context);
                await context.Database.MigrateAsync(InitialMigration);
                await InsertLegacyRowsAsync(context, [legacyRow]);
                await context.Database.ExecuteSqlRawAsync("DROP TABLE \"__EFMigrationsHistory\";");
            }

            using var provider = CreateBootstrapProvider();
            await DatabaseMigrationBootstrapper.MigrateAsync(provider);

            await using var assertionContext = _database.CreateContext();
            Assert.Equal([legacyRow], await ReadArchiveAsync(assertionContext));
            Assert.False(await TableExistsAsync(assertionContext, "CheckoutOrderItems"));
            Assert.Contains(CurrentMigration, await assertionContext.Database.GetAppliedMigrationsAsync());
        }

        [Fact]
        public async Task UpgradeFromIssue94WithNoLegacyRowsCreatesAnEmptyOperationalArchive()
        {
            await _database.ResetDatabaseAsync();
            await using var context = _database.CreateContext();
            await context.Database.MigrateAsync(PreviousMigration);

            await context.Database.MigrateAsync();

            Assert.False(await TableExistsAsync(context, "CheckoutOrderItems"));
            Assert.True(await TableExistsAsync(context, ArchiveTable));
            Assert.Empty(await ReadArchiveAsync(context));
        }

        [Fact]
        public async Task UpgradeFromIssue94PreservesEveryLegacyValueAndExistingCommerceState()
        {
            await _database.ResetDatabaseAsync();
            await using var context = _database.CreateContext();
            await context.Database.MigrateAsync(PreviousMigration);

            var duplicateProductId = Guid.NewGuid();
            var duplicateTimestamp = new DateTime(2024, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            var expectedArchive = new[]
            {
                new ArchivedCheckoutRow(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    2,
                    "customer-1",
                    new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc)),
                new ArchivedCheckoutRow(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    -17,
                    null,
                    new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
                new ArchivedCheckoutRow(Guid.NewGuid(), duplicateProductId, 0, "duplicate", duplicateTimestamp),
                new ArchivedCheckoutRow(Guid.NewGuid(), duplicateProductId, 0, "duplicate", duplicateTimestamp),
            };
            await InsertLegacyRowsAsync(context, expectedArchive);

            var orderId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var transactionId = Guid.NewGuid();
            var reservationId = Guid.NewGuid();
            var consumedOn = new DateTime(2024, 4, 5, 6, 7, 8, DateTimeKind.Utc);
            var categoryId = Guid.NewGuid();
            context.Categories.Add(new Category
            {
                Id = categoryId,
                Name = "Issue 95",
                Slug = $"issue-95-{categoryId:N}",
            });
            context.Products.Add(new Product
            {
                Id = productId,
                CategoryId = categoryId,
                Name = "Live product that must remain unchanged",
                Slug = $"issue-95-product-{productId:N}",
                Price = 25m,
                Quantity = 11,
            });
            context.Orders.Add(new Order
            {
                Id = orderId,
                UserId = "customer-1",
                Reference = "ORDER-FIRST-95",
                OrderStatus = OrderStatus.Confirmed,
                PaymentStatus = OrderPaymentStatus.Paid,
                PaymentMethod = OrderPaymentMethod.Stripe,
                FulfillmentStatus = FulfillmentStatus.NotStarted,
                TotalAmount = 50m,
                SubtotalAmount = 50m,
                Currency = "EUR",
                CustomerNameSnapshot = "Historical Customer",
                CustomerEmailSnapshot = "historical@example.com",
                CreatedOn = new DateTime(2024, 4, 5, 6, 0, 0, DateTimeKind.Utc),
                Lines =
                [
                    new OrderLine
                    {
                        ProductId = productId,
                        ProductNameSnapshot = "Historical Product",
                        SkuSnapshot = "SKU-HISTORY",
                        SizeScaleSnapshot = "ShoesUS",
                        SizeValueSnapshot = "10",
                        ColorSnapshot = "Black",
                        Quantity = 2,
                        UnitPrice = 25m,
                        LineTotal = 50m,
                    },
                ],
                PaymentTransactions =
                [
                    new PaymentTransaction
                    {
                        Id = transactionId,
                        Provider = PaymentProviderNames.Stripe,
                        ProviderSessionId = "cs_issue_95",
                        ExpectedAmountMinor = 5000,
                        Currency = "EUR",
                        Status = PaymentTransactionStatus.Paid,
                        PaidOn = consumedOn,
                    },
                ],
            });
            context.InventoryReservations.Add(new InventoryReservation
            {
                Id = reservationId,
                OrderId = orderId,
                ProductId = productId,
                Quantity = 2,
                Status = InventoryReservationStatus.Consumed,
                ConsumedOn = consumedOn,
            });
            await context.SaveChangesAsync();

            await context.Database.MigrateAsync();
            context.ChangeTracker.Clear();

            Assert.Equal(
                expectedArchive.OrderBy(row => row.Id),
                (await ReadArchiveAsync(context)).OrderBy(row => row.Id));
            Assert.False(await TableExistsAsync(context, "CheckoutOrderItems"));
            Assert.Equal(1, await context.Orders.CountAsync());

            var orderRepository = new OrderRepository(context);
            var queryService = new OrderQueryService(orderRepository, Mock.Of<IAppUserManager>());
            var history = Assert.Single(await queryService.GetOrdersForUserAsync("customer-1"));
            Assert.Equal("Historical Customer", history.CustomerName);
            Assert.Equal("historical@example.com", history.CustomerEmail);
            var line = Assert.Single(history.Lines);
            Assert.Equal("Historical Product", line.ProductName);
            Assert.Equal("SKU-HISTORY", line.Sku);
            Assert.Equal("ShoesUS", line.SizeScale);
            Assert.Equal("10", line.SizeValue);
            Assert.Equal("Black", line.Color);
            Assert.Equal(25m, line.UnitPrice);
            Assert.Equal(50m, line.LineTotal);

            var transaction = await context.PaymentTransactions.AsNoTracking()
                .SingleAsync(item => item.Id == transactionId);
            Assert.Equal(PaymentTransactionStatus.Paid, transaction.Status);
            Assert.Equal("cs_issue_95", transaction.ProviderSessionId);
            Assert.Equal(5000, transaction.ExpectedAmountMinor);
            var reservation = await context.InventoryReservations.AsNoTracking()
                .SingleAsync(item => item.Id == reservationId);
            Assert.Equal(InventoryReservationStatus.Consumed, reservation.Status);
            Assert.Equal(consumedOn, reservation.ConsumedOn);
            Assert.Null(reservation.ReleasedOn);
            Assert.Equal(
                11,
                await context.Products.AsNoTracking()
                    .Where(product => product.Id == productId)
                    .Select(product => product.Quantity)
                    .SingleAsync());

            var metrics = new MetricsService(orderRepository, Mock.Of<INewsletterSubscriberRepository>());
            var sales = await metrics.GetSalesAsync(
                new DateTime(2024, 4, 5, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2024, 4, 5, 23, 59, 0, DateTimeKind.Utc),
                BlazorShop.Application.DTOs.Analytics.MetricsGranularity.Day);
            Assert.Equal(50m, sales.Total);
        }

        [Fact]
        public async Task IdempotentMigrationScriptCanRunTwiceWithoutDuplicatingOrChangingArchive()
        {
            await _database.ResetDatabaseAsync();
            await using var context = _database.CreateContext();
            await context.Database.MigrateAsync(PreviousMigration);
            var expected = new ArchivedCheckoutRow(
                Guid.NewGuid(),
                Guid.NewGuid(),
                int.MinValue,
                null,
                new DateTime(2019, 8, 7, 6, 5, 4, DateTimeKind.Utc));
            await InsertLegacyRowsAsync(context, [expected]);

            var migrator = context.GetService<IMigrator>();
            var script = migrator.GenerateScript(
                PreviousMigration,
                CurrentMigration,
                MigrationsSqlGenerationOptions.Idempotent);

            await ExecuteScriptAsync(context, script);
            await ExecuteScriptAsync(context, script);

            Assert.Equal([expected], await ReadArchiveAsync(context));
            Assert.False(await TableExistsAsync(context, "CheckoutOrderItems"));
            Assert.Equal(
                1,
                await context.Database.SqlQuery<int>($$"""
                    SELECT COUNT(*)::integer AS "Value"
                    FROM "__EFMigrationsHistory"
                    WHERE "MigrationId" = {{CurrentMigration}}
                    """).SingleAsync());
        }

        private ServiceProvider CreateBootstrapProvider()
        {
            return new ServiceCollection()
                .AddLogging()
                .AddDbContext<AppDbContext>(options => options.UseNpgsql(
                    _database.ConnectionString,
                    npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)))
                .BuildServiceProvider();
        }

        private static Task ResetSchemaAsync(AppDbContext context)
        {
            return context.Database.ExecuteSqlRawAsync(
                """
                DROP SCHEMA IF EXISTS public CASCADE;
                CREATE SCHEMA public;
                """);
        }

        private static async Task InsertLegacyRowsAsync(
            AppDbContext context,
            IEnumerable<ArchivedCheckoutRow> rows)
        {
            foreach (var row in rows)
            {
                await context.Database.ExecuteSqlInterpolatedAsync($$"""
                    INSERT INTO "CheckoutOrderItems" ("Id", "ProductId", "Quantity", "UserId", "CreatedOn")
                    VALUES ({{row.Id}}, {{row.ProductId}}, {{row.Quantity}}, {{row.UserId}}, {{row.CreatedOn}})
                    """);
            }
        }

        private static async Task<IReadOnlyList<ArchivedCheckoutRow>> ReadArchiveAsync(AppDbContext context)
        {
            var connection = context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $$"""
                    SELECT "Id", "ProductId", "Quantity", "UserId", "CreatedOn"
                    FROM "{{ArchiveTable}}"
                    ORDER BY "Id"
                    """;
                await using var reader = await command.ExecuteReaderAsync();
                var rows = new List<ArchivedCheckoutRow>();
                while (await reader.ReadAsync())
                {
                    rows.Add(new ArchivedCheckoutRow(
                        reader.GetGuid(0),
                        reader.GetGuid(1),
                        reader.GetInt32(2),
                        reader.IsDBNull(3) ? null : reader.GetString(3),
                        reader.GetDateTime(4)));
                }

                return rows;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private static async Task<bool> TableExistsAsync(AppDbContext context, string tableName)
        {
            return await context.Database.SqlQuery<bool>($$"""
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public' AND table_name = {{tableName}})
                    AS "Value"
                """).SingleAsync();
        }

        private static async Task ExecuteScriptAsync(AppDbContext context, string script)
        {
            var connection = context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = script;
                command.CommandTimeout = 120;
                await command.ExecuteNonQueryAsync();
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private sealed record ArchivedCheckoutRow(
            Guid Id,
            Guid ProductId,
            int Quantity,
            string? UserId,
            DateTime CreatedOn);
    }
}
