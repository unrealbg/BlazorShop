namespace BlazorShop.Tests.Infrastructure.PostgreSql
{
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    using Testcontainers.PostgreSql;

    using Xunit;

    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture>
    {
        public const string Name = "PostgreSQL inventory";
    }

    public sealed class PostgreSqlFixture : IAsyncLifetime
    {
        private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
            .Build();

        public string ConnectionString => _container.GetConnectionString();

        public async Task InitializeAsync()
        {
            await _container.StartAsync();
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }

        public async Task DisposeAsync()
        {
            await _container.DisposeAsync().AsTask();
        }

        public AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(ConnectionString, npgsql =>
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
                .Options;

            return new AppDbContext(options);
        }

        public async Task ResetDatabaseAsync()
        {
            await using var context = CreateContext();
            await context.Database.ExecuteSqlRawAsync("""
                TRUNCATE TABLE
                    "InventoryReservations",
                    "OrderLines",
                    "Orders",
                    "ProductVariants",
                    "Products",
                    "Categories"
                CASCADE;
                """);
        }
    }
}
