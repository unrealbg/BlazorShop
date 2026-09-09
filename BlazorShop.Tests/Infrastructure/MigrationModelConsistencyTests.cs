namespace BlazorShop.Tests.Infrastructure
{
    using BlazorShop.Application;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Infrastructure;
    using System.Reflection;

    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Migrations;
    using Microsoft.EntityFrameworkCore.Migrations.Operations;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;

    using Moq;

    using Xunit;

    public class MigrationModelConsistencyTests
    {
        [Fact]
        public void DatabaseFacade_DoesNotReportPendingModelChanges()
        {
            using var context = CreateContext();

            Assert.False(context.Database.HasPendingModelChanges());
        }

        [Fact]
        public void AddInfrastructure_DoesNotReportPendingModelChanges()
        {
            using var provider = new ServiceCollection()
                .AddInfrastructure(CreateConfiguration())
                .BuildServiceProvider();
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var differ = context.GetService<IMigrationsModelDiffer>();
            var modelRuntimeInitializer = context.GetService<IModelRuntimeInitializer>();
            var validationLogger = context.GetService<IDiagnosticsLogger<DbLoggerCategory.Model.Validation>>();
            var designTimeModel = context.GetService<IDesignTimeModel>().Model;
            var snapshot = CreateSnapshot();
            var snapshotModel = modelRuntimeInitializer.Initialize(snapshot.Model, designTime: true, validationLogger);
            var operations = differ.GetDifferences(snapshotModel.GetRelationalModel(), designTimeModel.GetRelationalModel());
            var hasPendingModelChanges = context.Database.HasPendingModelChanges();
            var operationDetails = operations.Select(DescribeOperation);

            Assert.False(
                hasPendingModelChanges,
                $"AddInfrastructure reported pending model changes. Operations: {string.Join(" | ", operationDetails)}");
        }

        [Fact]
        public void AddInfrastructure_ResolvesInventoryServices()
        {
            using var provider = new ServiceCollection()
                .AddInfrastructure(CreateConfiguration())
                .BuildServiceProvider();
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IInventoryReservationService>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IProductInventoryTopologyRepository>());
        }

        [Fact]
        public void AddApplicationAndInfrastructure_ResolveCheckoutOrchestrator()
        {
            var configuration = CreateConfiguration();
            var hostEnvironment = new Mock<IHostEnvironment>();
            hostEnvironment.SetupGet(environment => environment.EnvironmentName)
                .Returns(Environments.Development);
            using var provider = new ServiceCollection()
                .AddLogging()
                .AddSingleton(hostEnvironment.Object)
                .AddInfrastructure(configuration)
                .AddApplication(configuration)
                .BuildServiceProvider();
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICheckoutOrchestrator>());
        }

        [Fact]
        public void RuntimeModel_MatchesMigrationSnapshot()
        {
            using var context = CreateContext();
            var differ = context.GetService<IMigrationsModelDiffer>();
            var modelRuntimeInitializer = context.GetService<IModelRuntimeInitializer>();
            var validationLogger = context.GetService<IDiagnosticsLogger<DbLoggerCategory.Model.Validation>>();
            var designTimeModel = context.GetService<IDesignTimeModel>().Model;
            var snapshot = CreateSnapshot();
            var snapshotModel = modelRuntimeInitializer.Initialize(snapshot.Model, designTime: true, validationLogger);

            var operations = differ.GetDifferences(snapshotModel.GetRelationalModel(), designTimeModel.GetRelationalModel());

            Assert.True(
                operations.Count == 0,
                $"Runtime model differs from migration snapshot: {string.Join(", ", operations.Select(operation => operation.GetType().Name))}");
        }

        [Theory]
        [InlineData("BlazorShop.Infrastructure/Services/InventoryReservationService.cs")]
        [InlineData("BlazorShop.Infrastructure/Services/StripePaymentReconciliationService.cs")]
        [InlineData("BlazorShop.Infrastructure/Services/StripePaymentStateTransitionService.cs")]
        public void LockedOrderQueries_SelectConcurrencyToken(string relativePath)
        {
            var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), relativePath));

            Assert.Contains("SELECT *, xmin FROM \\\"Orders\\\"", source);
            Assert.DoesNotContain("SELECT * FROM \\\"Orders\\\"", source);
        }

        private static ModelSnapshot CreateSnapshot()
        {
            var assembly = typeof(AppDbContext).Assembly;
            var snapshotType = assembly.GetType("BlazorShop.Infrastructure.Migrations.AppDbContextModelSnapshot", throwOnError: true)!;
            return (ModelSnapshot)Activator.CreateInstance(snapshotType, nonPublic: true)!;
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(
                    "Host=localhost;Port=5432;Database=blazorshop;Username=postgres;Password=postgres",
                    npgsqlOptions =>
                    {
                        npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                        npgsqlOptions.EnableRetryOnFailure();
                    })
                .Options;

            return new AppDbContext(options);
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "BlazorShop.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Unable to locate BlazorShop.sln from the test output directory.");
        }

        private static IConfiguration CreateConfiguration()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=blazorshop;Username=postgres;Password=postgres",
                    ["JWT:Audience"] = "test-audience",
                    ["JWT:Issuer"] = "test-issuer",
                    ["JWT:Key"] = "abcdefghijklmnopqrstuvwxyz123456",
                    ["ClientApp:BaseUrl"] = "https://shop.example.com",
                    ["Commerce:Currency"] = "EUR",
                })
                .Build();
        }

        private static string DescribeOperation(MigrationOperation operation)
        {
            return operation switch
            {
                AlterColumnOperation alterColumn => $"AlterColumn({alterColumn.Table}.{alterColumn.Name}: {alterColumn.ColumnType}, old={alterColumn.OldColumn.ColumnType}, nullable={alterColumn.IsNullable}, oldNullable={alterColumn.OldColumn.IsNullable})",
                _ => operation.GetType().Name
            };
        }
    }
}
