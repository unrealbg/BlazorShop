namespace BlazorShop.Tests.Infrastructure.Services.Admin
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Application.DTOs.Admin.Users;
    using BlazorShop.Application.Services.Contracts.Admin;
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Services.Admin;

    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;

    using Moq;

    using Xunit;

    public class AdminUserServiceTests
    {
        [Fact]
        public async Task UpdateRolesAsync_CannotRemoveLastAdmin()
        {
            await using var fixture = await IdentityFixture.CreateAsync();
            var admin = await fixture.CreateUserAsync("admin-1", "admin@example.com", "Admin");

            var result = await fixture.Service.UpdateRolesAsync(admin.Id, new UpdateUserRolesDto { Roles = ["User"] }, "other-admin");

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.Conflict, result.ResponseType);
            Assert.Contains("last Admin", result.Message);
        }

        [Fact]
        public async Task LockAsync_CannotLockSelf()
        {
            await using var fixture = await IdentityFixture.CreateAsync();
            var admin = await fixture.CreateUserAsync("admin-1", "admin@example.com", "Admin");

            var result = await fixture.Service.LockAsync(admin.Id, new UserLockRequestDto(), admin.Id);

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.Conflict, result.ResponseType);
            Assert.Contains("own account", result.Message);
        }

        [Fact]
        public async Task UpdateRolesAsync_RejectsUnknownRoles()
        {
            await using var fixture = await IdentityFixture.CreateAsync();
            var user = await fixture.CreateUserAsync("user-1", "user@example.com", "User");

            var result = await fixture.Service.UpdateRolesAsync(user.Id, new UpdateUserRolesDto { Roles = ["Manager"] }, "admin-1");

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.ValidationError, result.ResponseType);
            Assert.Contains("does not exist", result.Message);
        }

        [Fact]
        public async Task GetUsersAsync_SearchesFiltersAndPaginates()
        {
            await using var fixture = await IdentityFixture.CreateAsync();
            await fixture.CreateUserAsync("admin-1", "admin@example.com", "Admin", "Main Admin");
            await fixture.CreateUserAsync("user-1", "customer@example.com", "User", "Customer One");
            var locked = await fixture.CreateUserAsync("user-2", "locked@example.com", "User", "Locked Customer");
            await fixture.UserManager.SetLockoutEndDateAsync(locked, DateTimeOffset.UtcNow.AddDays(2));

            var result = await fixture.Service.GetUsersAsync(new AdminUserQueryDto
            {
                SearchTerm = "locked",
                Role = "User",
                Locked = true,
                PageNumber = 1,
                PageSize = 10,
            });

            Assert.Single(result.Items);
            Assert.Equal("locked@example.com", result.Items[0].Email);
            Assert.True(result.Items[0].IsLocked);
        }

        private sealed class IdentityFixture : IAsyncDisposable
        {
            private readonly ServiceProvider _provider;

            private IdentityFixture(ServiceProvider provider, AdminUserService service, UserManager<AppUser> userManager)
            {
                _provider = provider;
                Service = service;
                UserManager = userManager;
            }

            public AdminUserService Service { get; }

            public UserManager<AppUser> UserManager { get; }

            public static async Task<IdentityFixture> CreateAsync()
            {
                var services = new ServiceCollection();
                services.AddLogging();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase($"admin-users-{Guid.NewGuid()}"));
                services.AddIdentityCore<AppUser>()
                    .AddRoles<IdentityRole>()
                    .AddEntityFrameworkStores<AppDbContext>();

                var provider = services.BuildServiceProvider();
                var context = provider.GetRequiredService<AppDbContext>();
                await context.Database.EnsureCreatedAsync();

                var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
                foreach (var role in new[] { "Admin", "User" })
                {
                    if (await roleManager.FindByNameAsync(role) is null)
                    {
                        await roleManager.CreateAsync(new IdentityRole(role));
                    }
                }

                var audit = new Mock<IAdminAuditService>();
                audit.Setup(service => service.LogAsync(It.IsAny<CreateAdminAuditLogDto>()))
                    .ReturnsAsync(new ServiceResponse<AdminAuditLogDto>(true)
                    {
                        Payload = new AdminAuditLogDto { Id = Guid.NewGuid() },
                        ResponseType = ServiceResponseType.Success,
                    });

                var service = new AdminUserService(
                    provider.GetRequiredService<UserManager<AppUser>>(),
                    roleManager,
                    context,
                    audit.Object);

                return new IdentityFixture(provider, service, provider.GetRequiredService<UserManager<AppUser>>());
            }

            public async Task<AppUser> CreateUserAsync(string id, string email, string role, string fullName = "")
            {
                var user = new AppUser
                {
                    Id = id,
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    EmailConfirmed = true,
                    CreatedOn = DateTime.UtcNow,
                };

                var createResult = await UserManager.CreateAsync(user);
                Assert.True(createResult.Succeeded, string.Join(" ", createResult.Errors.Select(error => error.Description)));
                var roleResult = await UserManager.AddToRoleAsync(user, role);
                Assert.True(roleResult.Succeeded, string.Join(" ", roleResult.Errors.Select(error => error.Description)));
                return user;
            }

            public async ValueTask DisposeAsync()
            {
                await _provider.DisposeAsync();
            }
        }
    }
}
