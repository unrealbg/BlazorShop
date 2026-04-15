namespace BlazorShop.Tests.Infrastructure.Repositories
{
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Repositories.Authentication;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;

    using Xunit;

    public class AppTokenManagerTests
    {
        [Fact]
        public async Task ValidateRefreshTokenAsync_UsesHashedStorage()
        {
            await using var context = CreateContext();
            var manager = CreateManager(context);
            await SeedUserAsync(context, "user-1");

            var rawRefreshToken = manager.GetReFreshToken();
            var refreshToken = manager.CreateRefreshToken("user-1", rawRefreshToken, "127.0.0.1", "tests");

            await manager.AddRefreshTokenAsync(refreshToken);

            var storedRefreshToken = await context.RefreshTokens.SingleAsync();

            Assert.NotEqual(rawRefreshToken, storedRefreshToken.TokenHash);
            Assert.True(await manager.ValidateRefreshTokenAsync(rawRefreshToken));
            Assert.False(await manager.ValidateRefreshTokenAsync("different-token"));
        }

        [Fact]
        public async Task RevokeRefreshTokenAsync_MarksTokenRevoked_AndPreventsReuse()
        {
            await using var context = CreateContext();
            var manager = CreateManager(context);
            await SeedUserAsync(context, "user-2");

            var rawRefreshToken = manager.GetReFreshToken();
            var refreshToken = manager.CreateRefreshToken("user-2", rawRefreshToken, "127.0.0.1", "tests");

            await manager.AddRefreshTokenAsync(refreshToken);
            await manager.RevokeRefreshTokenAsync(rawRefreshToken, "127.0.0.1", "replacement-token");

            var storedRefreshToken = await manager.GetRefreshTokenAsync(rawRefreshToken);

            Assert.NotNull(storedRefreshToken);
            Assert.NotNull(storedRefreshToken!.RevokedAtUtc);
            Assert.Equal("127.0.0.1", storedRefreshToken.RevokedByIp);
            Assert.False(string.IsNullOrWhiteSpace(storedRefreshToken.ReplacedByTokenHash));
            Assert.False(await manager.ValidateRefreshTokenAsync(rawRefreshToken));
        }

        [Fact]
        public async Task RevokeRefreshTokenFamilyAsync_RevokesReplacementChain()
        {
            await using var context = CreateContext();
            var manager = CreateManager(context);
            await SeedUserAsync(context, "user-3");

            var originalRefreshToken = manager.GetReFreshToken();
            var replacementRefreshToken = manager.GetReFreshToken();
            var originalToken = manager.CreateRefreshToken("user-3", originalRefreshToken, "127.0.0.1", "tests");
            var replacementToken = manager.CreateRefreshToken("user-3", replacementRefreshToken, "127.0.0.1", "tests");

            await manager.AddRefreshTokenAsync(originalToken);
            await manager.AddRefreshTokenAsync(replacementToken);
            await manager.RevokeRefreshTokenAsync(originalRefreshToken, "127.0.0.1", replacementRefreshToken);
            await manager.RevokeRefreshTokenFamilyAsync(originalRefreshToken, "127.0.0.1");

            var storedReplacementToken = await manager.GetRefreshTokenAsync(replacementRefreshToken);

            Assert.NotNull(storedReplacementToken);
            Assert.NotNull(storedReplacementToken!.RevokedAtUtc);
            Assert.Equal("127.0.0.1", storedReplacementToken.RevokedByIp);
            Assert.False(await manager.ValidateRefreshTokenAsync(replacementRefreshToken));
        }

        private static AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase($"refresh-token-tests-{Guid.NewGuid()}")
                .Options;

            return new AppDbContext(options);
        }

        private static AppTokenManager CreateManager(AppDbContext context)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWT:Audience"] = "test-audience",
                    ["JWT:Issuer"] = "test-issuer",
                    ["JWT:Key"] = "abcdefghijklmnopqrstuvwxyz123456",
                    ["Runtime:Security:RefreshTokenLifetimeDays"] = "14"
                })
                .Build();

            return new AppTokenManager(context, configuration);
        }

        private static async Task SeedUserAsync(AppDbContext context, string userId)
        {
            context.Users.Add(new AppUser
            {
                Id = userId,
                UserName = $"{userId}@example.com",
                Email = $"{userId}@example.com",
                NormalizedUserName = $"{userId}@EXAMPLE.COM",
                NormalizedEmail = $"{userId}@EXAMPLE.COM",
                SecurityStamp = Guid.NewGuid().ToString("N")
            });

            await context.SaveChangesAsync();
        }
    }
}