namespace BlazorShop.Tests.Infrastructure.PostgreSql
{
    using System.Data;
    using System.Diagnostics;

    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Migrations;

    using Npgsql;

    using Xunit;

    [Collection(PostgreSqlCollection.Name)]
    public sealed class LegacyCheckoutArchiveSafetyPostgreSqlTests
    {
        private const string PreviousMigration = "20260908215523_SeparateOrderPaymentAndFulfillmentStatuses";
        private const string ArchiveMigration = "20260909075202_ArchiveLegacyCheckoutOrderItems";
        private const string LegacyTable = "CheckoutOrderItems";
        private const string ArchiveTable = "LegacyCheckoutOrderItemsArchive";
        private const string ArchiveComment =
            "Operational archive of legacy checkout-history rows. Not an active order-history source.";
        private readonly PostgreSqlFixture _database;

        public LegacyCheckoutArchiveSafetyPostgreSqlTests(PostgreSqlFixture database)
        {
            _database = database;
        }

        [Fact]
        public async Task ConfiguredPostgreSqlImage_MatchesRunningServerMajorVersion()
        {
            await using var connection = new NpgsqlConnection(_database.ConnectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand("SHOW server_version;", connection);
            var serverVersion = (string)(await command.ExecuteScalarAsync())!;
            var configuredMajor = _database.Image.Split(':', 2)[1].Split('-', 2)[0].Split('.', 2)[0];

            Assert.StartsWith($"{configuredMajor}.", serverVersion, StringComparison.Ordinal);
        }

        [Fact]
        public async Task DowngradeAndReapply_PreserveRawRowsHistoryCommentsAndCommerceState()
        {
            await RunWithCurrentSchemaCleanupAsync(async () =>
            {
                await ResetToPreviousMigrationAsync();
                await using var context = _database.CreateContext();
                var expectedRows = CreateRepresentativeLegacyRows();
                await InsertLegacyRowsAsync(context, expectedRows);
                var commerce = await SeedCommerceStateAsync(context);
                var previousHistory = (await context.Database.GetAppliedMigrationsAsync()).ToArray();

                await context.Database.MigrateAsync(ArchiveMigration);
                await AssertArchiveBoundaryAsync(context, expectedRows, ArchiveComment);
                Assert.Equal(previousHistory.Append(ArchiveMigration), await context.Database.GetAppliedMigrationsAsync());
                await AssertCommerceStateAsync(context, commerce);

                await context.Database.MigrateAsync(PreviousMigration);
                await AssertLegacyBoundaryAsync(context, expectedRows);
                Assert.Equal(previousHistory, await context.Database.GetAppliedMigrationsAsync());
                await AssertCommerceStateAsync(context, commerce);

                await context.Database.MigrateAsync(ArchiveMigration);
                await AssertArchiveBoundaryAsync(context, expectedRows, ArchiveComment);
                Assert.Equal(previousHistory.Append(ArchiveMigration), await context.Database.GetAppliedMigrationsAsync());
                await AssertCommerceStateAsync(context, commerce);
            });
        }

        [Fact]
        public async Task GeneratedReverseSql_ExecutesArchiveToPreviousWithoutMutatingDataOrCommerceState()
        {
            await RunWithCurrentSchemaCleanupAsync(async () =>
            {
                await ResetToPreviousMigrationAsync();
                await using var context = _database.CreateContext();
                var expectedRows = CreateRepresentativeLegacyRows();
                await InsertLegacyRowsAsync(context, expectedRows);
                var commerce = await SeedCommerceStateAsync(context);
                var previousHistory = (await context.Database.GetAppliedMigrationsAsync()).ToArray();
                await context.Database.MigrateAsync(ArchiveMigration);

                var reverseSql = context.GetService<IMigrator>().GenerateScript(
                    ArchiveMigration,
                    PreviousMigration,
                    MigrationsSqlGenerationOptions.Default);
                Assert.Contains($"ALTER TABLE \"{ArchiveTable}\" RENAME TO \"{LegacyTable}\"", reverseSql);

                await ExecuteScriptAsync(context, reverseSql);

                await AssertLegacyBoundaryAsync(context, expectedRows);
                Assert.Equal(previousHistory, await context.Database.GetAppliedMigrationsAsync());
                await AssertCommerceStateAsync(context, commerce);
            });
        }

        [Fact]
        public Task InFlightLegacyWriterCommit_BlocksRealMigrationAndRowIsArchivedExactlyOnce() =>
            RunWriterScenarioAsync(commitWriter: true);

        [Fact]
        public Task InFlightLegacyWriterRollback_BlocksRealMigrationAndRowIsAbsent() =>
            RunWriterScenarioAsync(commitWriter: false);

        [Fact]
        public async Task CancelledBlockedMigration_LeavesOriginalSchemaAndHistoryThenRetrySucceeds()
        {
            await RunWithCurrentSchemaCleanupAsync(async () =>
            {
                await ResetToPreviousMigrationAsync();
                var committedRow = CreateLegacyRow(4, "committed-before-block");
                var blockedRow = CreateLegacyRow(5, "cancelled-writer");
                await using (var seedContext = _database.CreateContext())
                {
                    await InsertLegacyRowsAsync(seedContext, [committedRow]);
                }

                await using var blocker = new NpgsqlConnection(_database.ConnectionString);
                await blocker.OpenAsync();
                var blockerPid = await GetBackendPidAsync(blocker);
                await using var blockerTransaction = await blocker.BeginTransactionAsync();
                await InsertLegacyRowAsync(blocker, blockerTransaction, blockedRow);

                await using (var migrationContext = _database.CreateContext())
                {
                    await migrationContext.Database.OpenConnectionAsync();
                    var migrationPid = await GetBackendPidAsync(
                        (NpgsqlConnection)migrationContext.Database.GetDbConnection());
                    using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                    var migrationTask = migrationContext.Database.MigrateAsync(
                        ArchiveMigration,
                        cancellation.Token);

                    await WaitForBlockedArchiveLockAsync(migrationPid, blockerPid);
                    cancellation.Cancel();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(
                        () => migrationTask.WaitAsync(TimeSpan.FromSeconds(10)));
                }

                await using (var failedAttemptContext = _database.CreateContext())
                {
                    Assert.True(await TableExistsAsync(failedAttemptContext, LegacyTable));
                    Assert.False(await TableExistsAsync(failedAttemptContext, ArchiveTable));
                    Assert.DoesNotContain(
                        ArchiveMigration,
                        await failedAttemptContext.Database.GetAppliedMigrationsAsync());
                    AssertArchivedRows([committedRow], await ReadRowsAsync(failedAttemptContext, LegacyTable));
                }

                await blockerTransaction.RollbackAsync();

                await using var retryContext = _database.CreateContext();
                await retryContext.Database.MigrateAsync(ArchiveMigration);
                await AssertArchiveBoundaryAsync(retryContext, [committedRow], ArchiveComment);
                Assert.Equal(
                    1,
                    (await retryContext.Database.GetAppliedMigrationsAsync())
                        .Count(migration => migration == ArchiveMigration));
            });
        }

        private async Task RunWriterScenarioAsync(bool commitWriter)
        {
            await RunWithCurrentSchemaCleanupAsync(async () =>
            {
                await ResetToPreviousMigrationAsync();
                var existingRow = CreateLegacyRow(6, "existing");
                var inFlightRow = CreateLegacyRow(7, commitWriter ? "commit" : "rollback");
                await using (var seedContext = _database.CreateContext())
                {
                    await InsertLegacyRowsAsync(seedContext, [existingRow]);
                }

                await using var blocker = new NpgsqlConnection(_database.ConnectionString);
                await blocker.OpenAsync();
                var blockerPid = await GetBackendPidAsync(blocker);
                await using var blockerTransaction = await blocker.BeginTransactionAsync();
                await InsertLegacyRowAsync(blocker, blockerTransaction, inFlightRow);

                await using var migrationContext = _database.CreateContext();
                await migrationContext.Database.OpenConnectionAsync();
                var migrationPid = await GetBackendPidAsync(
                    (NpgsqlConnection)migrationContext.Database.GetDbConnection());
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var migrationTask = migrationContext.Database.MigrateAsync(
                    ArchiveMigration,
                    cancellation.Token);

                await WaitForBlockedArchiveLockAsync(migrationPid, blockerPid);
                if (commitWriter)
                {
                    await blockerTransaction.CommitAsync();
                }
                else
                {
                    await blockerTransaction.RollbackAsync();
                }

                await migrationTask.WaitAsync(TimeSpan.FromSeconds(15));

                var expectedRows = commitWriter
                    ? new[] { existingRow, inFlightRow }
                    : [existingRow];
                await AssertArchiveBoundaryAsync(migrationContext, expectedRows, ArchiveComment);
                Assert.Equal(
                    1,
                    (await migrationContext.Database.GetAppliedMigrationsAsync())
                        .Count(migration => migration == ArchiveMigration));
            });
        }

        private async Task RunWithCurrentSchemaCleanupAsync(Func<Task> testBody)
        {
            Exception? originalFailure = null;
            try
            {
                await testBody();
            }
            catch (Exception exception)
            {
                originalFailure = exception;
                throw;
            }
            finally
            {
                try
                {
                    await ResetSchemaAndMigrateAsync(ArchiveMigration);
                }
                catch when (originalFailure is not null)
                {
                    // Preserve the original test failure; the next isolated setup recreates public schema.
                }
            }
        }

        private Task ResetToPreviousMigrationAsync() => ResetSchemaAndMigrateAsync(PreviousMigration);

        private async Task ResetSchemaAndMigrateAsync(string targetMigration)
        {
            await using var context = _database.CreateContext();
            await context.Database.ExecuteSqlRawAsync(
                """
                DROP SCHEMA IF EXISTS public CASCADE;
                CREATE SCHEMA public;
                """);
            await context.Database.MigrateAsync(targetMigration);
        }

        private async Task WaitForBlockedArchiveLockAsync(int migrationPid, int blockerPid)
        {
            await using var observer = new NpgsqlConnection(_database.ConnectionString);
            await observer.OpenAsync();
            var stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(10))
            {
                await using var command = new NpgsqlCommand(
                    """
                    SELECT EXISTS (
                        SELECT 1
                        FROM pg_locks AS waiting
                        INNER JOIN pg_class AS relation ON relation.oid = waiting.relation
                        INNER JOIN pg_namespace AS schema ON schema.oid = relation.relnamespace
                        WHERE waiting.pid = @migration_pid
                          AND waiting.mode = 'AccessExclusiveLock'
                          AND waiting.granted = FALSE
                          AND schema.nspname = 'public'
                          AND relation.relname = 'CheckoutOrderItems'
                          AND @blocker_pid = ANY(pg_blocking_pids(@migration_pid)))
                    """,
                    observer);
                command.Parameters.AddWithValue("migration_pid", migrationPid);
                command.Parameters.AddWithValue("blocker_pid", blockerPid);
                if ((bool)(await command.ExecuteScalarAsync())!)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(25));
            }

            throw new TimeoutException(
                $"Migration backend {migrationPid} did not expose a blocked AccessExclusiveLock "
                + $"on {LegacyTable} held by backend {blockerPid} within the bounded observer window.");
        }

        private static async Task<int> GetBackendPidAsync(NpgsqlConnection connection)
        {
            await using var command = new NpgsqlCommand("SELECT pg_backend_pid();", connection);
            return (int)(await command.ExecuteScalarAsync())!;
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

        private static async Task InsertLegacyRowAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            ArchivedCheckoutRow row)
        {
            await using var command = new NpgsqlCommand(
                """
                INSERT INTO "CheckoutOrderItems" ("Id", "ProductId", "Quantity", "UserId", "CreatedOn")
                VALUES (@id, @product_id, @quantity, @user_id, @created_on)
                """,
                connection,
                transaction);
            command.Parameters.AddWithValue("id", row.Id);
            command.Parameters.AddWithValue("product_id", row.ProductId);
            command.Parameters.AddWithValue("quantity", row.Quantity);
            command.Parameters.AddWithValue("user_id", (object?)row.UserId ?? DBNull.Value);
            command.Parameters.AddWithValue("created_on", row.CreatedOn);
            await command.ExecuteNonQueryAsync();
        }

        private static async Task AssertArchiveBoundaryAsync(
            AppDbContext context,
            IReadOnlyCollection<ArchivedCheckoutRow> expectedRows,
            string expectedComment)
        {
            Assert.False(await TableExistsAsync(context, LegacyTable));
            Assert.True(await TableExistsAsync(context, ArchiveTable));
            AssertArchivedRows(expectedRows, await ReadRowsAsync(context, ArchiveTable));
            Assert.Equal(expectedComment, await GetTableCommentAsync(context, ArchiveTable));
        }

        private static async Task AssertLegacyBoundaryAsync(
            AppDbContext context,
            IReadOnlyCollection<ArchivedCheckoutRow> expectedRows)
        {
            Assert.True(await TableExistsAsync(context, LegacyTable));
            Assert.False(await TableExistsAsync(context, ArchiveTable));
            AssertArchivedRows(expectedRows, await ReadRowsAsync(context, LegacyTable));
            Assert.Null(await GetTableCommentAsync(context, LegacyTable));
        }

        private static void AssertArchivedRows(
            IReadOnlyCollection<ArchivedCheckoutRow> expectedRows,
            IReadOnlyCollection<ArchivedCheckoutRow> actualRows)
        {
            var expected = expectedRows.OrderBy(row => row.Id).ToArray();
            var actual = actualRows.OrderBy(row => row.Id).ToArray();
            Assert.Equal(expected.Length, actual.Length);

            for (var index = 0; index < expected.Length; index++)
            {
                Assert.Equal(expected[index].Id, actual[index].Id);
                Assert.Equal(expected[index].ProductId, actual[index].ProductId);
                Assert.Equal(expected[index].Quantity, actual[index].Quantity);
                Assert.Equal(expected[index].UserId, actual[index].UserId);

                // PostgreSQL stores timestamp values at microsecond precision. Compare the exact
                // persisted instant without treating unsupported sub-microsecond CLR ticks as data loss.
                Assert.Equal(
                    expected[index].CreatedOn.ToUniversalTime().Ticks / TimeSpan.TicksPerMicrosecond,
                    actual[index].CreatedOn.ToUniversalTime().Ticks / TimeSpan.TicksPerMicrosecond);
            }
        }

        private static async Task<IReadOnlyList<ArchivedCheckoutRow>> ReadRowsAsync(
            AppDbContext context,
            string tableName)
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
                command.CommandText = $"""
                    SELECT "Id", "ProductId", "Quantity", "UserId", "CreatedOn"
                    FROM "{tableName}"
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

        private static async Task<string?> GetTableCommentAsync(AppDbContext context, string tableName)
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
                command.CommandText =
                    "SELECT obj_description(to_regclass(@qualified_name), 'pg_class');";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "qualified_name";
                parameter.Value = $"\"public\".\"{tableName}\"";
                command.Parameters.Add(parameter);
                var result = await command.ExecuteScalarAsync();
                return result is null or DBNull ? null : (string)result;
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

        private static IReadOnlyList<ArchivedCheckoutRow> CreateRepresentativeLegacyRows()
        {
            var duplicateProductId = Guid.NewGuid();
            var duplicateTimestamp = new DateTime(2023, 4, 5, 6, 7, 8, DateTimeKind.Utc);
            return
            [
                CreateLegacyRow(1, "customer"),
                CreateLegacyRow(-23, null),
                CreateLegacyRow(0, string.Empty),
                new ArchivedCheckoutRow(Guid.NewGuid(), duplicateProductId, int.MaxValue, "duplicate", duplicateTimestamp),
                new ArchivedCheckoutRow(Guid.NewGuid(), duplicateProductId, int.MaxValue, "duplicate", duplicateTimestamp),
            ];
        }

        private static ArchivedCheckoutRow CreateLegacyRow(int quantity, string? userId)
        {
            return new ArchivedCheckoutRow(
                Guid.NewGuid(),
                Guid.NewGuid(),
                quantity,
                userId,
                new DateTime(2022, 3, 4, 5, 6, 7, DateTimeKind.Utc).AddTicks(quantity));
        }

        private static async Task<CommerceSeed> SeedCommerceStateAsync(AppDbContext context)
        {
            var seed = CommerceSeed.Create();
            context.Categories.Add(new Category
            {
                Id = seed.CategoryId,
                Name = "Archive safety",
                Slug = $"archive-safety-{seed.CategoryId:N}",
            });
            context.Products.Add(new Product
            {
                Id = seed.ProductId,
                CategoryId = seed.CategoryId,
                Name = "Unchanged product",
                Slug = $"archive-safety-product-{seed.ProductId:N}",
                Price = 31.25m,
                Quantity = 12,
                CreatedOn = seed.CreatedOn,
            });
            context.ProductVariants.Add(new ProductVariant
            {
                Id = seed.VariantId,
                ProductId = seed.ProductId,
                Sku = "ARCHIVE-SAFETY-US10",
                SizeScale = SizeScale.ShoesUS,
                SizeValue = "10",
                Color = "Blue",
                Price = 31.25m,
                Stock = 8,
                IsDefault = true,
            });
            context.Orders.Add(new Order
            {
                Id = seed.OrderId,
                UserId = "archive-customer",
                Reference = "ARCHIVE-SAFETY-ORDER",
                OrderStatus = OrderStatus.Confirmed,
                PaymentStatus = OrderPaymentStatus.Paid,
                PaymentMethod = OrderPaymentMethod.Stripe,
                FulfillmentStatus = FulfillmentStatus.InTransit,
                TotalAmount = 62.50m,
                SubtotalAmount = 62.50m,
                Currency = "EUR",
                CustomerNameSnapshot = "Purchase-time customer",
                CustomerEmailSnapshot = "purchase-time@example.com",
                CreatedOn = seed.CreatedOn,
                ShippedOn = seed.CreatedOn.AddHours(1),
                Lines =
                [
                    new OrderLine
                    {
                        Id = seed.OrderLineId,
                        ProductId = seed.ProductId,
                        ProductVariantId = seed.VariantId,
                        ProductNameSnapshot = "Purchase-time product",
                        SkuSnapshot = "PURCHASE-SKU",
                        SizeScaleSnapshot = "ShoesUS",
                        SizeValueSnapshot = "10",
                        ColorSnapshot = "Blue",
                        Quantity = 2,
                        UnitPrice = 31.25m,
                        LineTotal = 62.50m,
                    },
                ],
                PaymentTransactions =
                [
                    new PaymentTransaction
                    {
                        Id = seed.TransactionId,
                        Provider = PaymentProviderNames.Stripe,
                        ProviderSessionId = "cs_archive_safety",
                        ProviderPaymentIntentId = "pi_archive_safety",
                        ExpectedAmountMinor = 6250,
                        Currency = "EUR",
                        Status = PaymentTransactionStatus.Paid,
                        CreatedOn = seed.CreatedOn,
                        UpdatedOn = seed.PaidOn,
                        PaidOn = seed.PaidOn,
                    },
                ],
            });
            context.InventoryReservations.Add(new InventoryReservation
            {
                Id = seed.ReservationId,
                OrderId = seed.OrderId,
                ProductId = seed.ProductId,
                ProductVariantId = seed.VariantId,
                Quantity = 2,
                Status = InventoryReservationStatus.Consumed,
                CreatedOn = seed.CreatedOn,
                ConsumedOn = seed.PaidOn,
            });
            context.PaymentProviderEvents.Add(new PaymentProviderEvent
            {
                Id = seed.ProviderEventId,
                Provider = PaymentProviderNames.Stripe,
                ProviderEventId = "evt_archive_safety",
                PaymentTransactionId = seed.TransactionId,
                OrderId = seed.OrderId,
                EventType = "checkout.session.completed",
                ProviderCreatedOn = seed.CreatedOn,
                ProcessingOutcome = PaymentProviderEventOutcome.Processed,
                ReceivedOn = seed.PaidOn,
                ProcessedOn = seed.PaidOn,
            });
            context.CheckoutIdempotencyRecords.Add(new CheckoutIdempotencyRecord
            {
                Id = seed.IdempotencyRecordId,
                UserId = "archive-customer",
                IdempotencyKey = seed.IdempotencyKey,
                RequestFingerprint = new string('a', 64),
                PaymentMethodId = Guid.NewGuid(),
                State = CheckoutIdempotencyState.Completed,
                OrderId = seed.OrderId,
                OrderReference = "ARCHIVE-SAFETY-ORDER",
                OutcomeJson = "{\"success\":true}",
                OutcomeVersion = 1,
                CreatedOn = seed.CreatedOn,
                UpdatedOn = seed.PaidOn,
                CompletedOn = seed.PaidOn,
                ExpiresOn = seed.PaidOn.AddDays(1),
            });
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return seed;
        }

        private static async Task AssertCommerceStateAsync(AppDbContext context, CommerceSeed seed)
        {
            context.ChangeTracker.Clear();
            var order = await context.Orders.AsNoTracking()
                .Include(item => item.Lines)
                .SingleAsync(item => item.Id == seed.OrderId);
            Assert.Equal(OrderStatus.Confirmed, order.OrderStatus);
            Assert.Equal(OrderPaymentStatus.Paid, order.PaymentStatus);
            Assert.Equal(FulfillmentStatus.InTransit, order.FulfillmentStatus);
            Assert.Equal(62.50m, order.TotalAmount);
            Assert.Equal("Purchase-time customer", order.CustomerNameSnapshot);
            var line = Assert.Single(order.Lines);
            Assert.Equal(seed.OrderLineId, line.Id);
            Assert.Equal(seed.VariantId, line.ProductVariantId);
            Assert.Equal("Purchase-time product", line.ProductNameSnapshot);
            Assert.Equal("PURCHASE-SKU", line.SkuSnapshot);
            Assert.Equal("ShoesUS", line.SizeScaleSnapshot);
            Assert.Equal(31.25m, line.UnitPrice);
            Assert.Equal(62.50m, line.LineTotal);

            var transaction = await context.PaymentTransactions.AsNoTracking()
                .SingleAsync(item => item.Id == seed.TransactionId);
            Assert.Equal("cs_archive_safety", transaction.ProviderSessionId);
            Assert.Equal("pi_archive_safety", transaction.ProviderPaymentIntentId);
            Assert.Equal(6250, transaction.ExpectedAmountMinor);
            Assert.Equal(PaymentTransactionStatus.Paid, transaction.Status);
            Assert.Equal(seed.PaidOn, transaction.PaidOn);

            var providerEvent = await context.PaymentProviderEvents.AsNoTracking()
                .SingleAsync(item => item.Id == seed.ProviderEventId);
            Assert.Equal("evt_archive_safety", providerEvent.ProviderEventId);
            Assert.Equal(PaymentProviderEventOutcome.Processed, providerEvent.ProcessingOutcome);
            Assert.Equal(seed.TransactionId, providerEvent.PaymentTransactionId);

            var reservation = await context.InventoryReservations.AsNoTracking()
                .SingleAsync(item => item.Id == seed.ReservationId);
            Assert.Equal(InventoryReservationStatus.Consumed, reservation.Status);
            Assert.Equal(seed.PaidOn, reservation.ConsumedOn);
            Assert.Null(reservation.ReleasedOn);

            var idempotency = await context.CheckoutIdempotencyRecords.AsNoTracking()
                .SingleAsync(item => item.Id == seed.IdempotencyRecordId);
            Assert.Equal(seed.IdempotencyKey, idempotency.IdempotencyKey);
            Assert.Equal(CheckoutIdempotencyState.Completed, idempotency.State);
            Assert.Equal(seed.OrderId, idempotency.OrderId);
            Assert.Equal("{\"success\": true}", idempotency.OutcomeJson);

            Assert.Equal(
                12,
                await context.Products.AsNoTracking()
                    .Where(item => item.Id == seed.ProductId)
                    .Select(item => item.Quantity)
                    .SingleAsync());
            Assert.Equal(
                8,
                await context.ProductVariants.AsNoTracking()
                    .Where(item => item.Id == seed.VariantId)
                    .Select(item => item.Stock)
                    .SingleAsync());
            Assert.Equal(1, await context.Orders.CountAsync());
            Assert.Equal(1, await context.OrderLines.CountAsync());
            Assert.Equal(1, await context.PaymentTransactions.CountAsync());
            Assert.Equal(1, await context.PaymentProviderEvents.CountAsync());
            Assert.Equal(1, await context.InventoryReservations.CountAsync());
            Assert.Equal(1, await context.CheckoutIdempotencyRecords.CountAsync());
        }

        private sealed record ArchivedCheckoutRow(
            Guid Id,
            Guid ProductId,
            int Quantity,
            string? UserId,
            DateTime CreatedOn);

        private sealed record CommerceSeed(
            Guid CategoryId,
            Guid ProductId,
            Guid VariantId,
            Guid OrderId,
            Guid OrderLineId,
            Guid TransactionId,
            Guid ProviderEventId,
            Guid ReservationId,
            Guid IdempotencyRecordId,
            Guid IdempotencyKey,
            DateTime CreatedOn,
            DateTime PaidOn)
        {
            public static CommerceSeed Create()
            {
                var createdOn = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
                return new CommerceSeed(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    createdOn,
                    createdOn.AddMinutes(5));
            }
        }
    }
}
