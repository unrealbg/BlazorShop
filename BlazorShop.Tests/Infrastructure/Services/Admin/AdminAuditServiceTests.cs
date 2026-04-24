namespace BlazorShop.Tests.Infrastructure.Services.Admin
{
    using System.Security.Claims;

    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Services.Admin;

    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;

    using Xunit;

    public class AdminAuditServiceTests
    {
        [Fact]
        public async Task LogAsync_PersistsActorAndMetadata()
        {
            await using var context = CreateContext();
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "admin-1"),
                new Claim(ClaimTypes.Email, "admin@example.com"),
            ], "Test"));

            var service = new AdminAuditService(context, new HttpContextAccessor { HttpContext = httpContext });

            var result = await service.LogAsync(new CreateAdminAuditLogDto
            {
                Action = "User.Locked",
                EntityType = "User",
                EntityId = "user-1",
                Summary = "Locked user.",
                MetadataJson = "{\"reason\":\"test\"}",
            });

            Assert.True(result.Success);
            var entry = await context.AdminAuditLogs.SingleAsync();
            Assert.Equal("admin-1", entry.ActorUserId);
            Assert.Equal("admin@example.com", entry.ActorEmail);
            Assert.Equal("User.Locked", entry.Action);
            Assert.Equal("{\"reason\":\"test\"}", entry.MetadataJson);
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"admin-audit-{Guid.NewGuid()}")
                .Options;

            return new AppDbContext(options);
        }
    }
}
