namespace BlazorShop.Tests.Infrastructure.PostgreSql;

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using BlazorShop.Application.DTOs;
using BlazorShop.Application.DTOs.Admin.Users;
using BlazorShop.Application.DTOs.Demo;
using BlazorShop.Application.DTOs.UserIdentity;
using BlazorShop.Application.Options;
using BlazorShop.Domain.Entities.Identity;
using BlazorShop.Infrastructure.Data;
using BlazorShop.Infrastructure.Demo;
using BlazorShop.Infrastructure.Services;
using BlazorShop.Tests.TestUtilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static RefreshEligibilityHttpTests;

public sealed partial class RegistrationConfirmationHttpTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NonStrictRegistration_RetainsLocalConfirmationWithoutOutboxOrSmtp(bool requireAccount)
    {
        await using var smtp = new LoopbackSmtpSink(SmtpTestMode.ClosedPort);
        await using var api = await CreateHostAsync(smtp, requireAccount: requireAccount, requireEmail: false);
        var registration = NewRegistration();
        using var created = await RegisterAsync(api, registration);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.True((await FindUserAsync(registration.Email)).EmailConfirmed);
        await using var db = database.CreateContext();
        Assert.Equal(0, await db.RegistrationConfirmationDeliveries.CountAsync());
        using var login = await LoginAsync(api, registration);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.True(smtp.Messages.IsEmpty, "Non-strict registration sent mail.");
    }

    [Fact]
    public async Task Resend_CoalescesPendingWork_RequiresPasswordAndEnforcesCooldownAndWindowQuota()
    {
        var clock = new ConfirmationTestClock();
        await using var smtp = new LoopbackSmtpSink();
        await using var api = await CreateHostAsync(smtp, configure: services =>
        {
            ConfigureClock(services, clock);
            services.Configure<RegistrationConfirmationOptions>(options => { options.MaxRequestsPerWindow = 2; options.RequestWindowHours = 1; });
        });
        var legacy = await api.CreateUserAsync("User", confirmed: false);
        var registration = NewRegistration();
        registration.Email = legacy.Email;
        registration.Password = "Wrong-password-123!";
        using var wrong = await LoginAsync(api, registration);
        var genericMessage = (await AssertNoCredentialsAsync(wrong)).Message;
        await using (var db = database.CreateContext()) Assert.Equal(0, await db.RegistrationConfirmationDeliveries.CountAsync());
        registration.Password = IdentityHttpHost.Password;
        using var first = await LoginAsync(api, registration);
        Assert.Equal(genericMessage, (await AssertNoCredentialsAsync(first)).Message);
        var eventId = (await IntentAsync()).EventId;
        using var coalesced = await LoginAsync(api, registration);
        Assert.Equal(eventId, (await IntentAsync()).EventId);
        Assert.Equal(1, (await IntentAsync()).RequestsInWindow);
        Assert.Equal(1, await DispatchAsync(api));
        using var cooldown = await LoginAsync(api, registration);
        Assert.Equal(eventId, (await IntentAsync()).EventId);
        clock.Advance(TimeSpan.FromSeconds(61));
        using var second = await LoginAsync(api, registration);
        Assert.Equal(2, (await IntentAsync()).Generation);
        Assert.Equal(2, (await IntentAsync()).RequestsInWindow);
        Assert.Equal(1, await DispatchAsync(api));
        clock.Advance(TimeSpan.FromSeconds(61));
        using var limited = await LoginAsync(api, registration);
        Assert.Equal(2, (await IntentAsync()).Generation);
        Assert.Equal(ConfirmationDeliveryState.Delivered, (await IntentAsync()).State);
        clock.Advance(TimeSpan.FromHours(1));
        using var recovered = await LoginAsync(api, registration);
        Assert.Equal(3, (await IntentAsync()).Generation);
        Assert.Equal(1, (await IntentAsync()).RequestsInWindow);
        Assert.False((await FindUserAsync(registration.Email)).EmailConfirmed);
        Assert.True(smtp.Messages.Count == 2, "Resend bypassed delivery limits or dispatched in HTTP.");
    }

    [Theory]
    [InlineData("lock")]
    [InlineData("deactivate")]
    public async Task AdministrativeIneligibility_SuppressesDeliveryWithoutUnlockOrConfirmation(string action)
    {
        var clock = new ConfirmationTestClock();
        await using var smtp = new LoopbackSmtpSink();
        await using var api = await CreateHostAsync(smtp, configure: services => ConfigureClock(services, clock));
        var registration = NewRegistration();
        using var created = await RegisterAsync(api, registration);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var user = await FindUserAsync(registration.Email);
        var administrator = await api.LoginAsync(await api.CreateUserAsync("Admin"));
        using var locked = action == "lock"
            ? await api.AdminAsync(administrator, user.Id, "lock", new UserLockRequestDto())
            : await api.SendAsync(HttpMethod.Delete, $"/api/admin/users/{user.Id}", administrator);
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        Assert.Equal(1, await DispatchAsync(api));
        Assert.Equal(ConfirmationDeliveryState.Suppressed, (await IntentAsync()).State);
        using var blocked = await LoginAsync(api, registration);
        await AssertNoCredentialsAsync(blocked);
        var after = await FindUserAsync(registration.Email);
        Assert.False(after.EmailConfirmed);
        Assert.True(after.LockoutEnd > DateTimeOffset.UtcNow);
        Assert.True(smtp.Messages.IsEmpty, "Locked/deactivated account received confirmation mail.");
        if (action == "lock")
        {
            using var unlocked = await api.AdminAsync(administrator, user.Id, "unlock");
            Assert.Equal(HttpStatusCode.OK, unlocked.StatusCode);
            clock.Advance(TimeSpan.FromSeconds(61));
            using var request = await LoginAsync(api, registration);
            await AssertNoCredentialsAsync(request);
            Assert.Equal(1, await DispatchAsync(api));
            using var confirmation = await ConfirmAsync(api, smtp.ConfirmationLink());
            Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
            using var login = await LoginAsync(api, registration);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        }
    }

    [Fact]
    public async Task PasswordStampChange_SuppressesStaleIntentAndRecoveryUsesExistingIdentityRules()
    {
        var clock = new ConfirmationTestClock();
        await using var smtp = new LoopbackSmtpSink();
        await using var api = await CreateHostAsync(smtp, configure: services => ConfigureClock(services, clock));
        var registration = NewRegistration();
        using var created = await RegisterAsync(api, registration);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var userId = (await FindUserAsync(registration.Email)).Id;
        var oldClaim = await IntentAsync();
        const string replacementPassword = "Replacement-password-123!";
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByIdAsync(userId))!;
            Assert.True((await users.ChangePasswordAsync(user, registration.Password, replacementPassword)).Succeeded);
        }
        Assert.Equal(1, await DispatchAsync(api));
        Assert.Equal(ConfirmationDeliveryState.Suppressed, (await IntentAsync()).State);
        Assert.True(smtp.Messages.IsEmpty, "Stale security-stamp intent was delivered.");
        clock.Advance(TimeSpan.FromSeconds(61));
        using var oldPassword = await LoginAsync(api, registration);
        await AssertNoCredentialsAsync(oldPassword);
        Assert.Equal(oldClaim.EventId, (await IntentAsync()).EventId);
        registration.Password = replacementPassword;
        using var fresh = await LoginAsync(api, registration);
        await AssertNoCredentialsAsync(fresh);
        Assert.NotEqual(oldClaim.EventId, (await IntentAsync()).EventId);
        Assert.Equal(1, await DispatchAsync(api));
        using var confirmed = await ConfirmAsync(api, smtp.ConfirmationLink());
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        using var login = await LoginAsync(api, registration);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task ExpiredExistingEmailProviderToken_IsRejectedAndExplicitResendRecoversWithoutActivation()
    {
        await using var smtp = new LoopbackSmtpSink();
        await using var api = await CreateHostAsync(smtp);
        var registration = NewRegistration();
        using var created = await RegisterAsync(api, registration);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var userId = (await FindUserAsync(registration.Email)).Id;
        string expired;
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByIdAsync(userId))!;
            Assert.Equal(TokenOptions.DefaultEmailProvider, users.Options.Tokens.EmailConfirmationTokenProvider);
            var key = await users.CreateSecurityTokenAsync(user);
            var modifier = await new EmailTokenProvider<AppUser>().GetUserModifierAsync("EmailConfirmation", users, user);
            var step = (DateTimeOffset.UtcNow - DateTimeOffset.UnixEpoch).Ticks / TimeSpan.FromMinutes(3).Ticks - 20;
            // Historical RFC6238 counter, verified by the actual configured Identity provider.
            // Select a non-colliding historical code: short TOTP strings can repeat naturally.
            expired = string.Empty;
            for (var candidate = 0; candidate < 10; candidate++)
            {
                var value = HistoricalTotp(key, modifier, step--);
                if (!await users.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider, "EmailConfirmation", value)) { expired = value; break; }
            }
            Assert.True(expired.Length > 0, "Existing Identity provider accepted all historical test codes.");
        }
        using var rejected = await SendAsync(api, HttpMethod.Get, $"/api/authentication/confirm-email?userId={Uri.EscapeDataString(userId)}&token={Uri.EscapeDataString(expired)}");
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.False((await FindUserAsync(registration.Email)).EmailConfirmed);
        Assert.Equal(1, await DispatchAsync(api));
        using var recovered = await ConfirmAsync(api, smtp.ConfirmationLink());
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }

    [Fact]
    public async Task ExistingUnconfirmedAdministrator_CanRecoverConfirmationWithoutRoleChange()
    {
        await using var smtp = new LoopbackSmtpSink();
        await using var api = await CreateHostAsync(smtp);
        var administrator = await api.CreateUserAsync("Admin", confirmed: false);
        var registration = NewRegistration();
        registration.Email = administrator.Email;
        using var blocked = await LoginAsync(api, registration);
        await AssertNoCredentialsAsync(blocked);
        Assert.Equal(1, await DispatchAsync(api));
        using var confirmed = await ConfirmAsync(api, smtp.ConfirmationLink());
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        using var login = await LoginAsync(api, registration);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        await using var scope = api.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        Assert.Equal(["Admin"], await users.GetRolesAsync((await users.FindByIdAsync(administrator.Id))!));
    }

    [Fact]
    public async Task AlreadyConfirmedAccount_SuppressesPendingRetryWithoutSendingAgain()
    {
        await using var smtp = new LoopbackSmtpSink(SmtpTestMode.LostAcknowledgement);
        var clock = new ConfirmationTestClock();
        await using var api = await CreateHostAsync(smtp, configure: services => ConfigureClock(services, clock));
        var registration = NewRegistration();
        using var created = await RegisterAsync(api, registration);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(1, await DispatchAsync(api));
        using var confirmed = await ConfirmAsync(api, smtp.ConfirmationLink());
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(1, await DispatchAsync(api));
        Assert.Equal(ConfirmationDeliveryState.Suppressed, (await IntentAsync()).State);
        Assert.True(smtp.Messages.Count == 1, "Confirmed account was mailed again.");
    }

    [Fact]
    public async Task ConcurrentResend_CreatesOneNewGenerationWithOneBudgetCharge()
    {
        var clock = new ConfirmationTestClock();
        await using var smtp = new LoopbackSmtpSink();
        await using var api = await CreateHostAsync(smtp, configure: services => ConfigureClock(services, clock));
        var registration = NewRegistration();
        using var created = await RegisterAsync(api, registration);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(1, await DispatchAsync(api));
        clock.Advance(TimeSpan.FromSeconds(61));
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => LoginAsync(api, registration)));
        try { foreach (var response in responses) await AssertNoCredentialsAsync(response); }
        finally { foreach (var response in responses) response.Dispose(); }
        var row = await IntentAsync();
        Assert.Equal(2, row.Generation);
        Assert.Equal(2, row.RequestsInWindow);
        Assert.Equal(1, await DispatchAsync(api));
        Assert.True(smtp.Messages.Count == 2, "Concurrent resend duplicated delivery intent.");
    }

    [Fact]
    public async Task RequireEmailWithoutRequireAccount_StillCommitsIntentAndBlocksUnconfirmedLogin()
    {
        await using var smtp = new LoopbackSmtpSink();
        await using var api = await CreateHostAsync(smtp, requireAccount: false, requireEmail: true);
        var registration = NewRegistration();
        using var created = await RegisterAsync(api, registration);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(ConfirmationDeliveryState.Pending, (await IntentAsync()).State);
        using var login = await LoginAsync(api, registration);
        await AssertNoCredentialsAsync(login);
        Assert.False((await FindUserAsync(registration.Email)).EmailConfirmed);
    }

    [Fact]
    public async Task SupersededIssuedLink_FollowsExistingIdentityStampRules_ResendUsesCurrentEmail()
    {
        var clock = new ConfirmationTestClock();
        await using var smtp = new LoopbackSmtpSink();
        await using var api = await CreateHostAsync(smtp, configure: services => ConfigureClock(services, clock));
        var registration = NewRegistration();
        using var created = await RegisterAsync(api, registration);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal(1, await DispatchAsync(api));
        var original = smtp.ConfirmationLink();
        var oldToken = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(original.Query)["token"].ToString();
        var userId = (await FindUserAsync(registration.Email)).Id;
        var newEmail = $"updated-{Guid.NewGuid():N}@example.test";
        await using (var scope = api.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = (await users.FindByIdAsync(userId))!;
            Assert.True((await users.SetEmailAsync(user, newEmail)).Succeeded);
            // Select a superseded, non-colliding Identity code. Reissue itself does not
            // revoke unexpired TOTP strings; no new revocation policy is introduced.
            for (var collision = 0; collision < 10 && await users.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider, "EmailConfirmation", oldToken); collision++)
                Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);
            Assert.False(await users.VerifyUserTokenAsync(user, TokenOptions.DefaultEmailProvider, "EmailConfirmation", oldToken));
        }
        using var rejected = await ConfirmAsync(api, original);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.False((await FindUserAsync(newEmail)).EmailConfirmed);
        registration.Email = newEmail;
        clock.Advance(TimeSpan.FromSeconds(61));
        using var request = await LoginAsync(api, registration);
        await AssertNoCredentialsAsync(request);
        Assert.Equal(newEmail, (await IntentAsync()).EmailSnapshot);
        Assert.Equal(1, await DispatchAsync(api));
        using var confirmed = await ConfirmAsync(api, smtp.ConfirmationLink(1));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
    }

    [Fact]
    public async Task DemoContext_CannotCreateOrDispatchRealConfirmationWork()
    {
        await using var smtp = new LoopbackSmtpSink();
        await using var api = await CreateHostAsync(smtp, demoEnabled: true);
        using var realRegistration = await RegisterAsync(api, NewRegistration());
        Assert.Equal(HttpStatusCode.OK, realRegistration.StatusCode);
        var realIntent = await IntentAsync();
        using var start = await SendAsync(api, HttpMethod.Post, "/api/demo/sessions", new StartDemoSessionRequest { Role = "User" });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var cookie = start.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("blazorshop-demo-session=", StringComparison.Ordinal)).Split(';')[0];
        var attempted = NewRegistration();
        using (var request = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/create") { Content = JsonContent.Create(attempted) })
        {
            request.Headers.Add("Cookie", cookie);
            using var response = await api.SendRawAsync(request);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        await using (var lease = await api.Services.GetRequiredService<IDemoSessionManager>().AcquireAsync(Uri.UnescapeDataString(cookie[(cookie.IndexOf('=') + 1)..])))
        {
            Assert.NotNull(lease);
            using var active = api.Services.GetRequiredService<DemoRequestContext>().Activate(lease.Context);
            Assert.Equal(0, await DispatchAsync(api));
            await using var scope = api.Services.CreateAsyncScope();
            Assert.Null(await scope.ServiceProvider.GetRequiredService<RegistrationConfirmationStore>().ClaimAsync(CancellationToken.None));
        }
        await using var db = database.CreateContext();
        Assert.Equal(0, await db.Users.CountAsync(user => user.Email == attempted.Email));
        Assert.Equal(realIntent.EventId, (await IntentAsync()).EventId);
        Assert.Equal(0, (await IntentAsync()).AttemptCount);
        Assert.True(smtp.Messages.IsEmpty, "Demo context dispatched real email.");
        Assert.Equal(1, await DispatchAsync(api));
    }

    [Fact]
    public async Task HostedWorker_ResumesCommittedIntentAfterHostRestart()
    {
        await using var smtp = new LoopbackSmtpSink();
        var registration = NewRegistration();
        await using (var first = await CreateHostAsync(smtp))
        {
            using var created = await RegisterAsync(first, registration);
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            Assert.True(smtp.Messages.IsEmpty, "Disabled hosted worker sent mail.");
        }
        await using var restarted = await CreateHostAsync(smtp, configure: services =>
            services.Configure<RegistrationConfirmationOptions>(options => { options.WorkerEnabled = true; options.PollSeconds = 1; options.BatchSize = 1; }));
        await smtp.DataReceived.Task.WaitAsync(TimeSpan.FromSeconds(15));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while ((await IntentAsync()).State != ConfirmationDeliveryState.Delivered) await Task.Delay(50, timeout.Token);
        using var confirm = await ConfirmAsync(restarted, smtp.ConfirmationLink());
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
    }

    private static string HistoricalTotp(byte[] key, string modifier, long step)
    {
        var data = BitConverter.GetBytes(IPAddress.HostToNetworkOrder(step)).Concat(Encoding.UTF8.GetBytes(modifier)).ToArray();
        var hash = HMACSHA1.HashData(key, data);
        var offset = hash[^1] & 15;
        var binary = ((hash[offset] & 127) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }
}
