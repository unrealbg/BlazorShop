namespace BlazorShop.Tests.Presentation.API
{
    using BlazorShop.API;
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    using Xunit;

    public class AdminBootstrapperTests
    {
        [Fact]
        public async Task BootstrapAsync_CreatesConfirmedAdmin_FromConfiguration()
        {
            await using var fixture = await BootstrapFixture.CreateAsync();
            var configuration = CreateConfiguration("admin@example.com", "StrongPass123!", "Initial Admin");

            await AdminBootstrapper.BootstrapAsync(fixture.Services, configuration);

            var user = await fixture.UserManager.FindByEmailAsync("admin@example.com");
            Assert.NotNull(user);
            Assert.Equal("Initial Admin", user.FullName);
            Assert.True(user.EmailConfirmed);
            Assert.True(await fixture.UserManager.IsInRoleAsync(user, "Admin"));
            Assert.False(await fixture.UserManager.IsInRoleAsync(user, "User"));
        }

        [Fact]
        public async Task BootstrapAsync_RefusesRerun_WhenAdminAlreadyExists()
        {
            await using var fixture = await BootstrapFixture.CreateAsync();
            var configuration = CreateConfiguration("admin@example.com", "StrongPass123!", "Initial Admin");
            await AdminBootstrapper.BootstrapAsync(fixture.Services, configuration);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => AdminBootstrapper.BootstrapAsync(fixture.Services, configuration));

            Assert.Contains("already exists", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Single(await fixture.UserManager.GetUsersInRoleAsync("Admin"));
            Assert.Single(fixture.UserManager.Users);
        }

        [Fact]
        public async Task BootstrapAsync_DoesNotPromoteExistingUser()
        {
            await using var fixture = await BootstrapFixture.CreateAsync();
            var existingUser = new AppUser
            {
                UserName = "customer@example.com",
                Email = "customer@example.com",
                FullName = "Customer",
                EmailConfirmed = true,
            };
            Assert.True((await fixture.UserManager.CreateAsync(existingUser, "StrongPass123!")).Succeeded);
            Assert.True((await fixture.UserManager.AddToRoleAsync(existingUser, "User")).Succeeded);

            var configuration = CreateConfiguration("customer@example.com", "DifferentPass123!", "Initial Admin");
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => AdminBootstrapper.BootstrapAsync(fixture.Services, configuration));

            Assert.Contains("existing account", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(await fixture.UserManager.IsInRoleAsync(existingUser, "Admin"));
            Assert.Empty(await fixture.UserManager.GetUsersInRoleAsync("Admin"));
        }

        [Fact]
        public async Task BootstrapAsync_RequiresSecureConfiguration()
        {
            await using var fixture = await BootstrapFixture.CreateAsync();
            var configuration = new ConfigurationBuilder().Build();

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => AdminBootstrapper.BootstrapAsync(fixture.Services, configuration));

            Assert.Contains("AdminBootstrap:Email", exception.Message);
            Assert.Empty(fixture.UserManager.Users);
        }

        private static IConfiguration CreateConfiguration(string email, string password, string fullName)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AdminBootstrap:Email"] = email,
                    ["AdminBootstrap:Password"] = password,
                    ["AdminBootstrap:FullName"] = fullName,
                })
                .Build();
        }

        private sealed class BootstrapFixture : IAsyncDisposable
        {
            private readonly ServiceProvider _provider;

            private BootstrapFixture(ServiceProvider provider)
            {
                _provider = provider;
                Services = provider;
                UserManager = provider.GetRequiredService<UserManager<AppUser>>();
            }

            public IServiceProvider Services { get; }

            public UserManager<AppUser> UserManager { get; }

            public static async Task<BootstrapFixture> CreateAsync()
            {
                var services = new ServiceCollection();
                var databaseName = $"admin-bootstrap-{Guid.NewGuid()}";
                services.AddLogging();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
                services.AddIdentityCore<AppUser>()
                    .AddRoles<IdentityRole>()
                    .AddEntityFrameworkStores<AppDbContext>();

                var provider = services.BuildServiceProvider();
                var context = provider.GetRequiredService<AppDbContext>();
                await context.Database.EnsureCreatedAsync();

                var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
                foreach (var roleName in new[] { "Admin", "User" })
                {
                    if (await roleManager.RoleExistsAsync(roleName))
                    {
                        continue;
                    }

                    var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                    Assert.True(roleResult.Succeeded, string.Join(" ", roleResult.Errors.Select(error => error.Description)));
                }

                return new BootstrapFixture(provider);
            }

            public async ValueTask DisposeAsync()
            {
                await _provider.DisposeAsync();
            }
        }
    }
}
