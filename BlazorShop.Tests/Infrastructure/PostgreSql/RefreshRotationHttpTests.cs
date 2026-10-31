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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitFailure_FailsClosedWithoutRetryOrUnlinkedSuccessor(bool afterCommit)
    {
        var fault = new CommitFault(afterCommit);
        await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString, fault);
        var user = await host.CreateUserAsync("User");
        var session = await host.LoginAsync(user);
        fault.Arm();
        using var response = await host.RefreshAsync(session);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertRejectedAsync(response);
        Assert.Equal(1, fault.Attempts);
        await using var scope = host.Services.CreateAsyncScope();
        var tokens = await scope.ServiceProvider.GetRequiredService<AppDbContext>().RefreshTokens.AsNoTracking()
            .Where(token => token.UserId == user.Id).ToListAsync();
        Assert.Equal(afterCommit ? 2 : 1, tokens.Count);
        if (afterCommit)
        {
            var parent = Assert.Single(tokens, token => token.ReplacedByTokenHash is not null);
            Assert.Contains(tokens, child => child.TokenHash == parent.ReplacedByTokenHash);
            // The server cannot recover the lost secret. Explicit reuse is still replay.
            using var replay = await host.RefreshAsync(session);
            Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
            await host.AssertAllRevokedAsync(user);
        }
        else
        {
            Assert.Null(tokens[0].RevokedAtUtc);
            Assert.Null(tokens[0].ReplacedByTokenHash);
        }
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("invalid")]
    [InlineData("missing")]
    public async Task InactiveCredential_NeverIssuesReplacement(string state)
    {
        await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString);
        var user = await host.CreateUserAsync("User");
        var session = await host.LoginAsync(user);
        await using (var scope = host.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (state == "expired") await context.RefreshTokens.Where(token => token.UserId == user.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(token => token.ExpiresAtUtc, DateTime.UtcNow.AddDays(-1)));
            if (state == "revoked") await context.RefreshTokens.Where(token => token.UserId == user.Id)
                .ExecuteUpdateAsync(update => update.SetProperty(token => token.RevokedAtUtc, DateTime.UtcNow));
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/refresh-token");
        if (state != "missing") request.Headers.Add("Cookie", state == "invalid" ? "__Host-blazorshop-refresh=invalid" : session.Cookie);
        using var response = await host.SendRawAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertRejectedAsync(response);
        Assert.Equal(1, await host.TokenCountAsync(user));
    }

    [Fact]
    public async Task LostResponse_ExplicitRetryRevokesSuccessorAndPreservesIndependentFamily()
    {
        await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString);
        var user = await host.CreateUserAsync("User");
        var original = await host.LoginAsync(user);
        var independent = await host.LoginAsync(user);
        // Dispose a committed response without delivering its cookie/JWT to the caller.
        using (var lost = await host.RefreshAsync(original)) Assert.Equal(HttpStatusCode.OK, lost.StatusCode);
        using var retry = await host.RefreshAsync(original);
        Assert.Equal(HttpStatusCode.BadRequest, retry.StatusCode);
        await AssertRejectedAsync(retry);
        Assert.Equal(3, await host.TokenCountAsync(user));
        await host.RotateAsync(independent);
    }

    [Fact]
    public async Task LogoutOfOldCredential_RevokesItsCurrentDescendantOnly()
    {
        await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString);
        var user = await host.CreateUserAsync("User");
        var original = await host.LoginAsync(user);
        var current = await host.RotateAsync(await host.RotateAsync(original));
        var independent = await host.LoginAsync(user);
        using var logout = await host.SendAsync(HttpMethod.Post, "/api/authentication/logout", original, useCookie: true);
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        using var denied = await host.RefreshAsync(current);
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        await host.RotateAsync(independent);
    }

    private sealed class CommitFault(bool afterCommit) : DbTransactionInterceptor
    {
        private int armed;
        public int Attempts { get; private set; }
        public void Arm() => Volatile.Write(ref armed, 1);
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref armed) == 1)
            {
                Attempts++;
                if (!afterCommit) { Volatile.Write(ref armed, 0); throw new TimeoutException("Injected pre-commit failure."); }
            }
            return ValueTask.FromResult(result);
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (afterCommit && Interlocked.Exchange(ref armed, 0) == 1) throw new TimeoutException("Injected lost commit acknowledgement.");
            return Task.CompletedTask;
        }
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
