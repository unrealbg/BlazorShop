namespace BlazorShop.API
{
    using System.Data;

    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    using Serilog;

    internal static class DatabaseMigrationBootstrapper
    {
        private const string EfProductVersion = "10.0.0";
        private const string MigrationHistoryTableName = "__EFMigrationsHistory";

        private static readonly string[] InitialSchemaTables =
        [
            "AspNetRoles",
            "AspNetUsers",
            "AspNetRoleClaims",
            "AspNetUserClaims",
            "AspNetUserLogins",
            "AspNetUserRoles",
            "AspNetUserTokens",
            "Categories",
            "CheckoutOrderItems",
            "NewsletterSubscribers",
            "Orders",
            "OrderLines",
            "PaymentMethods",
            "Products",
            "ProductVariants",
            "RefreshTokens"
        ];

        private static readonly IReadOnlyDictionary<string, string[]> InitialSchemaColumns =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["AspNetRoles"] = new[] { "Id", "Name", "NormalizedName", "ConcurrencyStamp" },
                ["AspNetUsers"] = new[] { "Id", "FullName", "UserName", "NormalizedUserName", "Email", "NormalizedEmail", "EmailConfirmed",
                    "PasswordHash", "SecurityStamp", "ConcurrencyStamp", "PhoneNumber", "PhoneNumberConfirmed",
                    "TwoFactorEnabled", "LockoutEnd", "LockoutEnabled", "AccessFailedCount" },
                ["AspNetRoleClaims"] = new[] { "Id", "RoleId", "ClaimType", "ClaimValue" },
                ["AspNetUserClaims"] = new[] { "Id", "UserId", "ClaimType", "ClaimValue" },
                ["AspNetUserLogins"] = new[] { "LoginProvider", "ProviderKey", "ProviderDisplayName", "UserId" },
                ["AspNetUserRoles"] = new[] { "UserId", "RoleId" },
                ["AspNetUserTokens"] = new[] { "UserId", "LoginProvider", "Name", "Value" },
                ["Categories"] = new[] { "Id", "Name" },
                ["CheckoutOrderItems"] = new[] { "Id", "ProductId", "Quantity", "UserId", "CreatedOn" },
                ["NewsletterSubscribers"] = new[] { "Id", "Email", "CreatedOn" },
                ["Orders"] = new[] { "Id", "UserId", "Status", "Reference", "TotalAmount", "CreatedOn", "ShippingCarrier",
                    "TrackingNumber", "TrackingUrl", "ShippingStatus", "ShippedOn", "DeliveredOn", "LastTrackingUpdate" },
                ["OrderLines"] = new[] { "Id", "OrderId", "ProductId", "Quantity", "UnitPrice" },
                ["PaymentMethods"] = new[] { "Id", "Name" },
                ["Products"] = new[] { "Id", "Name", "Description", "Price", "Image", "Quantity", "CreatedOn", "CategoryId" },
                ["ProductVariants"] = new[] { "Id", "ProductId", "Sku", "SizeScale", "SizeValue", "Price", "Stock", "Color", "IsDefault" },
                ["RefreshTokens"] = new[] { "Id", "UserId", "Token" }
            };

        private static readonly IReadOnlyDictionary<string, string[]> InitialSchemaIndexes =
            new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["AspNetRoles"] = new[] { "RoleNameIndex" },
                ["AspNetUsers"] = new[] { "EmailIndex", "UserNameIndex" },
                ["AspNetRoleClaims"] = new[] { "IX_AspNetRoleClaims_RoleId" },
                ["AspNetUserClaims"] = new[] { "IX_AspNetUserClaims_UserId" },
                ["AspNetUserLogins"] = new[] { "IX_AspNetUserLogins_UserId" },
                ["AspNetUserRoles"] = new[] { "IX_AspNetUserRoles_RoleId" },
                ["NewsletterSubscribers"] = new[] { "IX_NewsletterSubscribers_Email" },
                ["OrderLines"] = new[] { "IX_OrderLines_OrderId" },
                ["Products"] = new[] { "IX_Products_CategoryId" },
                ["ProductVariants"] = new[] { "IX_ProductVariants_ProductId_SizeScale_SizeValue" }
            };

        public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        {
            using var scope = services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await BaselineLegacySchemaAsync(dbContext, cancellationToken);
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        private static async Task BaselineLegacySchemaAsync(AppDbContext dbContext, CancellationToken cancellationToken)
        {
            var existingTables = await GetExistingPublicTablesAsync(dbContext, cancellationToken);

            var hasMigrationHistoryTable = existingTables.Contains(MigrationHistoryTableName);
            if (hasMigrationHistoryTable && await HasAppliedMigrationsAsync(dbContext, cancellationToken))
            {
                return;
            }

            existingTables.Remove(MigrationHistoryTableName);

            if (existingTables.Count == 0)
            {
                return;
            }

            await ValidateLegacyInitialSchemaAsync(dbContext, existingTables, cancellationToken);

            var initialMigrationId = dbContext.Database.GetMigrations().FirstOrDefault();
            if (string.IsNullOrWhiteSpace(initialMigrationId))
            {
                return;
            }

            Log.Warning(
                "Existing schema detected without EF migration history. Marking migration {MigrationId} as applied.",
                initialMigrationId);

            if (!hasMigrationHistoryTable)
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    @"CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                        ""MigrationId"" character varying(150) NOT NULL,
                        ""ProductVersion"" character varying(32) NOT NULL,
                        CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
                    );",
                    cancellationToken);
            }

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
                   VALUES ({initialMigrationId}, {EfProductVersion})
                   ON CONFLICT (""MigrationId"") DO NOTHING;",
                cancellationToken);
        }

        private static async Task ValidateLegacyInitialSchemaAsync(
            AppDbContext dbContext,
            HashSet<string> existingTables,
            CancellationToken cancellationToken)
        {
            var missingTables = InitialSchemaTables
                .Where(table => !existingTables.Contains(table))
                .OrderBy(table => table, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (missingTables.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Existing database schema cannot be auto-baselined because required tables are missing: {string.Join(", ", missingTables)}. Recreate the development database or add migration history manually after verifying the schema.");
            }

            var existingColumns = await GetExistingPublicTableColumnsAsync(dbContext, cancellationToken);
            var existingIndexes = await GetExistingPublicIndexesAsync(dbContext, cancellationToken);
            var schemaIssues = new List<string>();

            foreach (var table in InitialSchemaColumns)
            {
                if (!existingColumns.TryGetValue(table.Key, out var actualColumns))
                {
                    schemaIssues.Add($"Table '{table.Key}' is missing from column metadata.");
                    continue;
                }

                var missingColumns = table.Value
                    .Where(column => !actualColumns.Contains(column))
                    .OrderBy(column => column, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (missingColumns.Length > 0)
                {
                    schemaIssues.Add($"Table '{table.Key}' is missing columns: {string.Join(", ", missingColumns)}.");
                }
            }

            foreach (var table in InitialSchemaIndexes)
            {
                existingIndexes.TryGetValue(table.Key, out var actualIndexes);
                actualIndexes ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var missingIndexes = table.Value
                    .Where(index => !actualIndexes.Contains(index))
                    .OrderBy(index => index, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (missingIndexes.Length > 0)
                {
                    schemaIssues.Add($"Table '{table.Key}' is missing indexes: {string.Join(", ", missingIndexes)}.");
                }
            }

            if (schemaIssues.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Existing database schema cannot be auto-baselined safely. {string.Join(" ", schemaIssues)} Recreate the development database or add migration history manually after verifying the schema.");
            }
        }

        private static async Task<bool> HasAppliedMigrationsAsync(AppDbContext dbContext, CancellationToken cancellationToken)
        {
            var connection = dbContext.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await dbContext.Database.OpenConnectionAsync(cancellationToken);
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"SELECT COUNT(*)
FROM ""__EFMigrationsHistory"";";

                var result = await command.ExecuteScalarAsync(cancellationToken);
                return result is not null && Convert.ToInt32(result) > 0;
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await dbContext.Database.CloseConnectionAsync();
                }
            }
        }

        private static async Task<HashSet<string>> GetExistingPublicTablesAsync(
            AppDbContext dbContext,
            CancellationToken cancellationToken)
        {
            var connection = dbContext.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await dbContext.Database.OpenConnectionAsync(cancellationToken);
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"SELECT table_name
FROM information_schema.tables
WHERE table_schema = 'public' AND table_type = 'BASE TABLE';";

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                while (await reader.ReadAsync(cancellationToken))
                {
                    tableNames.Add(reader.GetString(0));
                }

                return tableNames;
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await dbContext.Database.CloseConnectionAsync();
                }
            }
        }

        private static async Task<Dictionary<string, HashSet<string>>> GetExistingPublicTableColumnsAsync(
            AppDbContext dbContext,
            CancellationToken cancellationToken)
        {
            var connection = dbContext.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await dbContext.Database.OpenConnectionAsync(cancellationToken);
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = 'public';";

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var tableColumns = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var tableName = reader.GetString(0);
                    var columnName = reader.GetString(1);

                    if (!tableColumns.TryGetValue(tableName, out var columns))
                    {
                        columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        tableColumns[tableName] = columns;
                    }

                    columns.Add(columnName);
                }

                return tableColumns;
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await dbContext.Database.CloseConnectionAsync();
                }
            }
        }

        private static async Task<Dictionary<string, HashSet<string>>> GetExistingPublicIndexesAsync(
            AppDbContext dbContext,
            CancellationToken cancellationToken)
        {
            var connection = dbContext.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await dbContext.Database.OpenConnectionAsync(cancellationToken);
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"SELECT tablename, indexname
FROM pg_indexes
WHERE schemaname = 'public';";

                using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var tableIndexes = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var tableName = reader.GetString(0);
                    var indexName = reader.GetString(1);

                    if (!tableIndexes.TryGetValue(tableName, out var indexes))
                    {
                        indexes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        tableIndexes[tableName] = indexes;
                    }

                    indexes.Add(indexName);
                }

                return tableIndexes;
            }
            finally
            {
                if (shouldCloseConnection)
                {
                    await dbContext.Database.CloseConnectionAsync();
                }
            }
        }
    }
}