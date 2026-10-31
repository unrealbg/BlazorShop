namespace BlazorShop.Tests.Infrastructure.PostgreSql;

using System.Data.Common;
using System.Net;
using BlazorShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static RefreshEligibilityHttpTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class RefreshRotationHttpTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task ConcurrentSameCredential_ExactlyOneSuccessAndNoFork()
    {
        var barrier = new InitialReadBarrier();
        await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString, barrier);
        var user = await host.CreateUserAsync("User");
        var session = await host.LoginAsync(user);
        barrier.Arm();
        var responses = await Task.WhenAll(host.RefreshAsync(session), host.RefreshAsync(session));
        try
        {
            // Compare statuses only: never include credentials in assertion diagnostics.
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest));
            await using var scope = host.Services.CreateAsyncScope();
            var tokens = await scope.ServiceProvider.GetRequiredService<AppDbContext>().RefreshTokens
                .AsNoTracking().Where(token => token.UserId == user.Id).ToListAsync();
            Assert.Equal(2, tokens.Count);
            var parent = Assert.Single(tokens, token => token.ReplacedByTokenHash is not null);
            Assert.Contains(tokens, child => parent.ReplacedByTokenHash == child.TokenHash);
            // Strict replay revokes the committed successor; there is deliberately no grace window.
            Assert.All(tokens, token => Assert.NotNull(token.RevokedAtUtc));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    // Both requests finish their initial read before either proceeds. This reproduces the
    // old check/add/revoke race without sleeps, and also works with a locked atomic re-read.
    private sealed class InitialReadBarrier : DbCommandInterceptor
    {
        private int armed;
        private int arrivals;
        private readonly TaskCompletionSource bothRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Arm() => Volatile.Write(ref armed, 1);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,
            CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref armed) == 1 && command.CommandText.Contains("FROM \"RefreshTokens\"", StringComparison.Ordinal)
                && !command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
            {
                var arrival = Interlocked.Increment(ref arrivals);
                if (arrival == 2) bothRead.TrySetResult();
                if (arrival <= 2) await bothRead.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
