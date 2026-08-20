namespace BlazorShop.Tests.Infrastructure.PostgreSql
{
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;

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
            return new AppDbContext(CreateOptions());
        }

        public IDbContextFactory<AppDbContext> CreateContextFactory(params IInterceptor[] interceptors)
        {
            return new TestAppDbContextFactory(CreateOptions(interceptors));
        }

        private DbContextOptions<AppDbContext> CreateOptions(params IInterceptor[] interceptors)
        {
            var builder = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(ConnectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure();
                });

            if (interceptors.Length > 0)
            {
                builder.AddInterceptors(interceptors);
            }

            return builder.Options;
        }

        private sealed class TestAppDbContextFactory : IDbContextFactory<AppDbContext>
        {
            private readonly DbContextOptions<AppDbContext> _options;

            public TestAppDbContextFactory(DbContextOptions<AppDbContext> options)
            {
                _options = options;
            }

            public AppDbContext CreateDbContext() => new(_options);

            public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(CreateDbContext());
        }

        public async Task ResetDatabaseAsync()
        {
            await using var context = CreateContext();
            await context.Database.ExecuteSqlRawAsync("""
                TRUNCATE TABLE
                    "CheckoutIdempotencyRecords",
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
