namespace BlazorShop.Tests.Infrastructure.PostgreSql;

using System.Net;
using System.Text.RegularExpressions;
using BlazorShop.Application.Options;
using BlazorShop.Domain.Entities.Identity;
using BlazorShop.Infrastructure.Services;
using BlazorShop.Tests.TestUtilities;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using static RefreshEligibilityHttpTests;

public sealed partial class RegistrationConfirmationHttpTests
{
    [Fact]
    public async Task SmtpOutage_BoundedBackoffDeadLetterAndExplicitRecoveryKeepOneAccount()
    {
        var clock = new ConfirmationTestClock();
        var registration = NewRegistration();
        string userId;
        await using (var closed = new LoopbackSmtpSink(SmtpTestMode.ClosedPort))
        await using (var api = await CreateHostAsync(closed, configure: services => ConfigureClock(services, clock, maxAttempts: 3)))
        {
            using var created = await RegisterAsync(api, registration);
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            userId = (await FindUserAsync(registration.Email)).Id;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                var started = clock.GetUtcNow().UtcDateTime;
                Assert.Equal(1, await DispatchAsync(api));
                var row = await IntentAsync();
                Assert.Equal(attempt, row.AttemptCount);
                Assert.Equal(attempt == 3 ? ConfirmationDeliveryState.DeadLetter : ConfirmationDeliveryState.Pending, row.State);
                var delay = attempt == 1 ? 2 : 3;
                Assert.Equal(started.AddSeconds(delay), row.NextAttemptOn);
                Assert.Equal(0, await DispatchAsync(api));
                clock.Advance(TimeSpan.FromSeconds(delay));
            }
            clock.Advance(TimeSpan.FromHours(1));
            Assert.Equal(0, await DispatchAsync(api));
            Assert.False((await FindUserAsync(registration.Email)).EmailConfirmed);
            using var duplicate = await RegisterAsync(api, registration);
            Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
            Assert.Equal(3, (await IntentAsync()).AttemptCount);
        }
        await using var smtp = new LoopbackSmtpSink();
        await using var recovery = await CreateHostAsync(smtp, configure: services => ConfigureClock(services, clock, 3));
        using var blocked = await LoginAsync(recovery, registration);
        await AssertNoCredentialsAsync(blocked);
        var queued = await IntentAsync();
        Assert.Equal(ConfirmationDeliveryState.Pending, queued.State);
        Assert.Equal(2, queued.Generation);
        Assert.Equal(0, queued.AttemptCount);
        Assert.Equal(userId, queued.UserId);
        Assert.Equal(1, await DispatchAsync(recovery));
        using var confirmed = await ConfirmAsync(recovery, smtp.ConfirmationLink());
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        using var login = await LoginAsync(recovery, registration);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task AcceptedDataLostAck_RetryCanDuplicateMailButOriginalLinkAndAccountRemainValid()
    {
        var clock = new ConfirmationTestClock();
        await using var smtp = new LoopbackSmtpSink(SmtpTestMode.LostAcknowledgement);
        await using var api = await CreateHostAsync(smtp, configure: services => ConfigureClock(services, clock));
        var registration = NewRegistration();
        using var created = await RegisterAsync(api, registration);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(1, await DispatchAsync(api));
        Assert.Equal(ConfirmationDeliveryState.Pending, (await IntentAsync()).State);
        var originalLink = smtp.ConfirmationLink();
        smtp.Mode = SmtpTestMode.Healthy;
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(1, await DispatchAsync(api));
        Assert.True(smtp.Messages.Count == 2, "Expected at-least-once SMTP uncertainty, not duplicate accounts.");
        Assert.Equal(ConfirmationDeliveryState.Delivered, (await IntentAsync()).State);
        Assert.False((await FindUserAsync(registration.Email)).EmailConfirmed);
        using var confirmed = await ConfirmAsync(api, originalLink);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        await using var db = database.CreateContext();
        Assert.Equal(1, await db.Users.CountAsync(user => user.Email == registration.Email));
        Assert.Equal(1, await db.RegistrationConfirmationDeliveries.CountAsync());
    }

    [Fact]
    public async Task ClaimedWorkSurvivesHostRestartAndExpiredLease_OldCompletionIsFenced()
    {
        var clock = new ConfirmationTestClock();
        var registration = NewRegistration();
        RegistrationConfirmationDelivery oldClaim;
        await using var smtp = new LoopbackSmtpSink();
        await using (var api = await CreateHostAsync(smtp, configure: services => ConfigureClock(services, clock)))
        {
            using var created = await RegisterAsync(api, registration);
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            await using var scope = api.Services.CreateAsyncScope();
            oldClaim = (await scope.ServiceProvider.GetRequiredService<RegistrationConfirmationStore>().ClaimAsync(CancellationToken.None))!;
            Assert.NotNull(oldClaim);
            Assert.True(smtp.Messages.IsEmpty, "Claiming a lease dispatched mail.");
        }
        clock.Advance(TimeSpan.FromSeconds(61));
        await using var restarted = await CreateHostAsync(smtp, configure: services => ConfigureClock(services, clock));
        Assert.Equal(1, await DispatchAsync(restarted));
        await using (var scope = restarted.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RegistrationConfirmationStore>()
                .FinishAsync(oldClaim, ConfirmationDeliveryState.Pending, "stale-worker", CancellationToken.None);
        var row = await IntentAsync();
        Assert.Equal(ConfirmationDeliveryState.Delivered, row.State);
        Assert.Equal(2, row.AttemptCount);
        Assert.Null(row.LeaseOwnerId);
        Assert.True(smtp.Messages.Count == 1, "Restart lost ownership fencing.");
        using var confirmed = await ConfirmAsync(restarted, smtp.ConfirmationLink());
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    [Fact]
    public async Task ConcurrentWorkers_DoNotShareAnUnexpiredLease()
    {
        await using var smtp = new LoopbackSmtpSink(SmtpTestMode.HoldAcknowledgement);
        await using var api = await CreateHostAsync(smtp);
        using var created = await RegisterAsync(api, NewRegistration());
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var first = DispatchAsync(api);
        try
        {
            await smtp.DataReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, await DispatchAsync(api).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(ConfirmationDeliveryState.Processing, (await IntentAsync()).State);
        }
        finally { smtp.ReleaseAcknowledgement.TrySetResult(); }
        Assert.Equal(1, await first);
        Assert.True(smtp.Messages.Count == 1, "Two workers dispatched the same live lease.");
        Assert.Equal(ConfirmationDeliveryState.Delivered, (await IntentAsync()).State);
    }

    [Fact]
    public async Task LeaseExpiryDuringSmtp_CanDuplicateDeliveryButLateAckCannotOverwriteNewOwner()
    {
        var clock = new ConfirmationTestClock();
        await using var smtp = new LoopbackSmtpSink(SmtpTestMode.HoldAcknowledgement);
        await using var api = await CreateHostAsync(smtp, configure: services => ConfigureClock(services, clock));
        var registration = NewRegistration();
        using var created = await RegisterAsync(api, registration);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var first = DispatchAsync(api);
        try
        {
            await smtp.DataReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            clock.Advance(TimeSpan.FromSeconds(61));
            smtp.Mode = SmtpTestMode.Healthy;
            Assert.Equal(1, await DispatchAsync(api).WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(ConfirmationDeliveryState.Delivered, (await IntentAsync()).State);
        }
        finally { smtp.ReleaseAcknowledgement.TrySetResult(); }
        Assert.Equal(1, await first);
        var row = await IntentAsync();
        Assert.Equal(2, row.AttemptCount);
        Assert.Equal(ConfirmationDeliveryState.Delivered, row.State);
        Assert.Null(row.LeaseOwnerId);
        Assert.True(smtp.Messages.Count == 2, "Expected bounded at-least-once delivery after lease expiry.");
        Assert.False((await FindUserAsync(registration.Email)).EmailConfirmed);
    }

    [Fact]
    public async Task DisabledEmail_PreservesUnconfirmedAccountAndPendingIntentWithoutPretendingDelivery()
    {
        await using var smtp = new LoopbackSmtpSink();
        var registration = NewRegistration();
        await using (var disabled = await CreateHostAsync(smtp, enabled: false))
        {
            using var created = await RegisterAsync(disabled, registration);
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            Assert.Equal(0, await DispatchAsync(disabled));
            using var login = await LoginAsync(disabled, registration);
            await AssertNoCredentialsAsync(login);
            Assert.Equal(ConfirmationDeliveryState.Pending, (await IntentAsync()).State);
            Assert.Equal(0, (await IntentAsync()).AttemptCount);
            Assert.True(smtp.Messages.IsEmpty, "Disabled delivery emitted mail.");
        }
        await using var enabled = await CreateHostAsync(smtp);
        Assert.Equal(1, await DispatchAsync(enabled));
        using var confirm = await ConfirmAsync(enabled, smtp.ConfirmationLink());
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
    }

    [Fact]
    public async Task LongQueuedIntent_GeneratesUsableCurrentIdentityTokenRatherThanPersistingAnExpiredLink()
    {
        await using var smtp = new LoopbackSmtpSink();
        await using var api = await CreateHostAsync(smtp);
        using var created = await RegisterAsync(api, NewRegistration());
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        await using (var db = database.CreateContext())
            await db.RegistrationConfirmationDeliveries.ExecuteUpdateAsync(update => update
                .SetProperty(row => row.CreatedOn, DateTime.UtcNow.AddDays(-2)).SetProperty(row => row.NextAttemptOn, DateTime.UtcNow.AddDays(-2)));
        Assert.DoesNotContain(typeof(RegistrationConfirmationDelivery).GetProperties(), property =>
            property.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) || property.Name is "Body" or "Link" or "Password");
        Assert.Equal(1, await DispatchAsync(api));
        using var confirm = await ConfirmAsync(api, smtp.ConfirmationLink());
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
    }

    [Fact]
    public async Task MaliciousSmtpResponseCannotLeakConfirmationPayloadIntoWorkerLogs()
    {
        await using var smtp = new LoopbackSmtpSink(SmtpTestMode.EchoPayloadRejection);
        await using var api = await CreateHostAsync(smtp);
        using var logs = new ConfirmationLogCapture();
        api.Services.GetRequiredService<ILoggerFactory>().AddProvider(logs);
        using var created = await RegisterAsync(api, NewRegistration());
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(1, await DispatchAsync(api));
        var link = smtp.ConfirmationLink();
        var token = QueryHelpers.ParseQuery(link.Query)["token"].ToString();
        var all = string.Join('\n', logs.Messages);
        Assert.True(!all.Contains("synthetic-rejection", StringComparison.Ordinal) && !all.Contains("token=", StringComparison.Ordinal)
            && !Regex.IsMatch(all, $"(?<![A-Za-z0-9]){Regex.Escape(token)}(?![A-Za-z0-9])"), "Confirmation payload appeared in logs.");
        Assert.Equal("delivery-failure", (await IntentAsync()).LastFailureCode);
    }

    private static void ConfigureClock(IServiceCollection services, ConfirmationTestClock clock, int maxAttempts = 5)
    {
        services.AddSingleton<TimeProvider>(clock);
        services.Configure<RegistrationConfirmationOptions>(options =>
        {
            options.MaxAttempts = maxAttempts;
            options.InitialRetrySeconds = 2;
            options.MaxRetrySeconds = 3;
            options.ResendCooldownSeconds = 60;
        });
    }

    private sealed class ConfirmationLogCapture : ILoggerProvider
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Capture(this);
        public void Dispose() { }
        private sealed class Capture(ConfirmationLogCapture owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.Messages.Enqueue(formatter(state, exception) + (exception?.ToString() ?? string.Empty));
        }
    }
}
