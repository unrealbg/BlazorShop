namespace BlazorShop.Tests.Infrastructure.PostgreSql;

using System.Data.Common;
using System.Net;
using BlazorShop.Application.Services.Contracts.Authentication;
using BlazorShop.Domain.Contracts.Authentication;
using BlazorShop.Domain.Entities.Identity;
using BlazorShop.Infrastructure.Data;
using BlazorShop.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using static RefreshEligibilityHttpTests;

public sealed partial class RegistrationConfirmationHttpTests
{
    [Fact]
    public async Task UncommittedUserRoleAndIntent_AreInvisibleToWorkerUntilTheSingleCommit()
    {
        await using var smtp = new LoopbackSmtpSink();
        var barrier = new CommitControl { Pause = true };
        await using var api = await CreateHostAsync(smtp, configure: services =>
            services.AddDbContext<AppDbContext>(options => options.AddInterceptors(barrier)));
        var registration = NewRegistration();
        var pending = RegisterAsync(api, registration);
        try
        {
            await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await using (var db = database.CreateContext())
            {
                Assert.Equal(0, await db.Users.CountAsync(user => user.Email == registration.Email));
                Assert.Equal(0, await db.RegistrationConfirmationDeliveries.CountAsync());
            }
            Assert.Equal(0, await DispatchAsync(api));
            Assert.True(smtp.Messages.IsEmpty, "SMTP observed an uncommitted intent.");
        }
        finally { barrier.Release.TrySetResult(); }
        using var response = await pending;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertCommittedCustomerAsync(registration.Email);
        Assert.Equal(1, await DispatchAsync(api));
        Assert.True(smtp.Messages.Count == 1, "Expected one post-commit message.");
    }

    [Theory]
    [InlineData("enqueue")]
    [InlineData("before-commit")]
    [InlineData("role")]
    public async Task FailureBeforeCommit_RollsBackIdentityRoleAndIntentWithoutSmtp(string failure)
    {
        await using var smtp = new LoopbackSmtpSink();
        var commit = new CommitControl { FailBefore = failure == "before-commit" };
        var enqueue = new EnqueueControl { FailInsert = failure == "enqueue" };
        await using var api = await CreateHostAsync(smtp, configure: services =>
        {
            services.AddDbContext<AppDbContext>(options => options.AddInterceptors(commit, enqueue));
            if (failure == "role")
            {
                var roles = new Mock<IAppRoleManager>();
                roles.Setup(manager => manager.AddUserToRoleAsync(It.IsAny<AppUser>(), "User")).ReturnsAsync(false);
                services.AddSingleton(roles.Object);
            }
        });
        var registration = NewRegistration();
        using var response = await RegisterAsync(api, registration);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var db = database.CreateContext();
        Assert.Equal(0, await db.Users.CountAsync(user => user.Email == registration.Email));
        Assert.Equal(0, await db.RegistrationConfirmationDeliveries.CountAsync());
        Assert.Equal(1, enqueue.UserInserts);
        Assert.True(smtp.Messages.IsEmpty, "Failed registration emitted SMTP.");
        Assert.Equal(0, await DispatchAsync(api));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostCommitAcknowledgement_UsesPrivateEventProofWithoutReplayOrAccountDeletion(bool verificationUnavailable)
    {
        await using var smtp = new LoopbackSmtpSink();
        var commit = new CommitControl { FailAfter = true };
        var enqueue = new EnqueueControl { FailRead = verificationUnavailable };
        await using var api = await CreateHostAsync(smtp, configure: services =>
            services.AddDbContext<AppDbContext>(options => options.AddInterceptors(commit, enqueue)));
        var registration = NewRegistration();
        using var response = await RegisterAsync(api, registration);
        Assert.Equal(verificationUnavailable ? HttpStatusCode.BadRequest : HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, commit.Failures);
        Assert.Equal(1, enqueue.UserInserts);
        await AssertCommittedCustomerAsync(registration.Email);
        Assert.True(smtp.Messages.IsEmpty, "SMTP occurred while registration commit was uncertain.");
        enqueue.FailRead = false;
        Assert.Equal(1, await DispatchAsync(api));
        using var confirm = await ConfirmAsync(api, smtp.ConfirmationLink());
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
    }

    [Fact]
    public async Task LostCommitAck_RegistrationProofSurvivesAnExplicitResendGeneration()
    {
        var clock = new ConfirmationTestClock();
        var commit = new CommitControl { FailAfter = true, PauseAfter = true };
        await using var smtp = new LoopbackSmtpSink();
        await using var api = await CreateHostAsync(smtp, configure: services =>
        {
            ConfigureClock(services, clock);
            services.AddDbContext<AppDbContext>(options => options.AddInterceptors(commit));
        });
        var registration = NewRegistration();
        var pending = RegisterAsync(api, registration);
        try
        {
            await commit.Committed.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var root = (await IntentAsync()).Id;
            Assert.Equal(1, await DispatchAsync(api));
            clock.Advance(TimeSpan.FromSeconds(61));
            using var resend = await LoginAsync(api, registration);
            await AssertNoCredentialsAsync(resend);
            var updated = await IntentAsync();
            Assert.Equal(root, updated.Id);
            Assert.NotEqual(root, updated.EventId);
            Assert.Equal(2, updated.Generation);
        }
        finally { commit.ReleaseAfter.TrySetResult(); }
        using var response = await pending;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = database.CreateContext();
        Assert.Equal(1, await db.Users.CountAsync(user => user.Email == registration.Email));
        Assert.Equal(1, await db.RegistrationConfirmationDeliveries.CountAsync());
    }

    [Fact]
    public async Task ConcurrentAndSequentialDuplicateRegistration_CreateOneCustomerRoleAndIntent()
    {
        await using var smtp = new LoopbackSmtpSink();
        await using var api = await CreateHostAsync(smtp);
        var registration = NewRegistration();
        var requests = await Task.WhenAll(RegisterAsync(api, registration), RegisterAsync(api, registration));
        try
        {
            Assert.Equal(1, requests.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.Equal(1, requests.Count(response => response.StatusCode == HttpStatusCode.BadRequest));
        }
        finally { foreach (var request in requests) request.Dispose(); }
        var before = await IntentAsync();
        using var duplicate = await RegisterAsync(api, registration);
        Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
        Assert.Equal(before.EventId, (await IntentAsync()).EventId);
        await AssertCommittedCustomerAsync(registration.Email);
        Assert.Equal(1, await DispatchAsync(api));
        Assert.True(smtp.Messages.Count == 1, "Duplicate registration scheduled another message.");
    }

    private async Task AssertCommittedCustomerAsync(string email)
    {
        await using var db = database.CreateContext();
        var user = await db.Users.SingleAsync(user => user.Email == email);
        Assert.False(user.EmailConfirmed);
        Assert.True(user.PasswordHash != IdentityHttpHost.Password, "Identity password was not hashed.");
        var role = await (from assignment in db.UserRoles join candidate in db.Roles on assignment.RoleId equals candidate.Id
            where assignment.UserId == user.Id select candidate.Name).SingleAsync();
        Assert.Equal("User", role);
        var intent = await db.RegistrationConfirmationDeliveries.SingleAsync();
        Assert.Equal(user.Id, intent.UserId);
        Assert.Equal(ConfirmationDeliveryState.Pending, intent.State);
        Assert.Equal(0, intent.AttemptCount);
        Assert.Equal(1, intent.Generation);
    }

    private sealed class CommitControl : DbTransactionInterceptor
    {
        public bool Pause { get; init; }
        public bool FailBefore { get; init; }
        public bool FailAfter { get; init; }
        public bool PauseAfter { get; init; }
        public int Failures;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseAfter { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            Reached.TrySetResult();
            if (Pause) await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            if (FailBefore) { Interlocked.Increment(ref Failures); throw new IOException("Synthetic failure before commit."); }
            return result;
        }

        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (FailAfter && Interlocked.Increment(ref Failures) == 1)
            {
                Committed.TrySetResult();
                if (PauseAfter) await ReleaseAfter.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                throw new IOException("Synthetic lost commit acknowledgement.");
            }
        }
    }

    private sealed class EnqueueControl : DbCommandInterceptor
    {
        public bool FailInsert { get; init; }
        public bool FailRead { get; set; }
        public int UserInserts;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO \"AspNetUsers\"", StringComparison.Ordinal)) Interlocked.Increment(ref UserInserts);
            if (FailInsert && command.CommandText.Contains("INSERT INTO \"RegistrationConfirmationDeliveries\"", StringComparison.Ordinal))
                throw new IOException("Synthetic enqueue failure.");
            if (FailRead && command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.Ordinal)
                && command.CommandText.Contains("RegistrationConfirmationDeliveries", StringComparison.Ordinal))
                throw new IOException("Synthetic verification unavailable.");
            return ValueTask.FromResult(result);
        }
    }
}
