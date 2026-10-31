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

    [Fact]
    public async Task ConcurrentLogoutAndRefresh_SerializesAndLeavesNoRenewableDescendant()
    {
        var barrier = new InitialReadBarrier();
        await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString, barrier);
        var user = await host.CreateUserAsync("User");
        var session = await host.LoginAsync(user);
        barrier.Arm();
        var refreshing = host.RefreshAsync(session);
        var loggingOut = host.SendAsync(HttpMethod.Post, "/api/authentication/logout", session, useCookie: true);
        using var refreshed = await refreshing;
        using var logout = await loggingOut;
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Contains(refreshed.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.BadRequest });
        Assert.InRange(await host.TokenCountAsync(user), 1, 2);
        await host.AssertAllRevokedAsync(user);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HttpCancellation_BeforeOrAfterCommitDoesNotDeliverCredentials(bool afterCommit)
    {
        var barrier = new CommitCancellation(afterCommit);
        await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString, barrier);
        var user = await host.CreateUserAsync("User");
        var session = await host.LoginAsync(user);
        barrier.Arm();
        using var cancellation = new CancellationTokenSource();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/refresh-token");
        request.Headers.Add("Cookie", session.Cookie);
        var refreshing = host.SendRawAsync(request, cancellation.Token);
        await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await refreshing);
        await barrier.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(30));
        // Wait for disposal/rollback using a real follow-up transaction, not a sleep.
        await using var scope = host.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await context.Database.BeginTransactionAsync();
            await context.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AspNetUsers\" WHERE \"Id\" = {user.Id} FOR UPDATE");
            Assert.Equal(afterCommit ? 2 : 1, await host.TokenCountAsync(user));
        });
    }

    private sealed class CommitCancellation(bool afterCommit) : DbTransactionInterceptor
    {
        private int armed;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Arm() => Volatile.Write(ref armed, 1);
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (!afterCommit && Interlocked.Exchange(ref armed, 0) == 1) await AwaitCancellationAsync(cancellationToken);
            return result;
        }
        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (afterCommit && Interlocked.Exchange(ref armed, 0) == 1) await AwaitCancellationAsync(cancellationToken);
        }
        private async Task AwaitCancellationAsync(CancellationToken cancellationToken)
        {
            Reached.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).WaitAsync(TimeSpan.FromSeconds(30)); }
            catch (OperationCanceledException) { Cancelled.TrySetResult(); throw; }
        }
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Admin")]
    public async Task DemoRotation_UsesLeasedStoreAndRejectsMissingOrReboundContext(string role)
    {
        await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString, demoEnabled: true);
        var normalUser = await host.CreateUserAsync(role);
        var normal = await host.LoginAsync(normalUser);
        var (credential, demoUser) = await SessionObservationHttpTests.StartDemoAsync(host, role);
        var original = await host.LoginAsync(demoUser, credential);
        async Task<HttpResponseMessage> RefreshDemoAsync(Session session, string? context)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/refresh-token");
            request.Headers.Add("Cookie", session.Cookie);
            if (context is not null) request.Headers.Add("X-BlazorShop-Demo-Session", context);
            return await host.SendRawAsync(request);
        }
        using var missing = await RefreshDemoAsync(original, null);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        using var rebound = await RefreshDemoAsync(normal, credential);
        Assert.Equal(HttpStatusCode.BadRequest, rebound.StatusCode);
        using var rotated = await RefreshDemoAsync(original, credential);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var successor = await IdentityHttpHost.ReadSessionAsync(rotated);
        using var replay = await RefreshDemoAsync(original, credential);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        using var revoked = await RefreshDemoAsync(successor, credential);
        Assert.Equal(HttpStatusCode.BadRequest, revoked.StatusCode);
        // Demo requests never mutate the ordinary account's session family.
        Assert.Equal(1, await host.TokenCountAsync(normalUser));
        await host.RotateAsync(normal);
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
