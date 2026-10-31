namespace BlazorShop.Tests.Infrastructure.PostgreSql
{
    using System.Net;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Data.Common;
    using System.Security.Cryptography;
    using System.Threading.RateLimiting;

    using BlazorShop.API.Controllers;
    using BlazorShop.API.Demo;
    using BlazorShop.API.Options;
    using BlazorShop.API.SessionObservation;
    using BlazorShop.Application;
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Users;
    using BlazorShop.Application.DTOs.UserIdentity;
    using BlazorShop.Application.Options;
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Infrastructure;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using Xunit;

    [Collection(PostgreSqlCollection.Name)]
    public sealed class RefreshEligibilityHttpTests(PostgreSqlFixture database)
    {
        [Theory]
        [InlineData("User")]
        [InlineData("Admin")]
        public async Task AdminLock_RejectsExistingRefreshCookieAndClearsIt(string role)
        {
            await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString);
            var customer = await host.CreateUserAsync(role);
            var admin = await host.CreateUserAsync("Admin");
            var session = await host.LoginAsync(customer);
            var administrator = await host.LoginAsync(admin);

            using var locked = await host.AdminAsync(administrator, customer.Id, "lock", new UserLockRequestDto());
            Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
            using var response = await host.RefreshAsync(session);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertRejectedAsync(response);
            await host.AssertAllRevokedAsync(customer);
            Assert.Equal(1, await host.TokenCountAsync(customer));
        }

        [Fact]
        public async Task AdminLock_RevokesEverySessionFamily_UnlockRequiresNewLogin()
        {
            await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString);
            var customer = await host.CreateUserAsync("User");
            var administrator = await host.LoginAsync(await host.CreateUserAsync("Admin"));
            var original = await host.LoginAsync(customer);
            var replacement = await host.RotateAsync(original);
            var secondBrowser = await host.LoginAsync(customer);
            var otherUser = await host.LoginAsync(await host.CreateUserAsync("User"));

            using var locked = await host.AdminAsync(administrator, customer.Id, "lock", new UserLockRequestDto());
            Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
            await host.AssertAllRevokedAsync(customer);
            using var unlocked = await host.AdminAsync(administrator, customer.Id, "unlock");
            Assert.Equal(HttpStatusCode.OK, unlocked.StatusCode);
            foreach (var session in new[] { original, replacement, secondBrowser })
            {
                using var response = await host.RefreshAsync(session);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                await AssertRejectedAsync(response);
            }

            await host.RotateAsync(otherUser);
            await host.RotateAsync(await host.LoginAsync(customer));
        }

        [Fact]
        public async Task Deactivate_RevokesSessionsAndDeniesLoginAndRefresh()
        {
            await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString);
            var customer = await host.CreateUserAsync("User");
            var session = await host.LoginAsync(customer);
            var administrator = await host.LoginAsync(await host.CreateUserAsync("Admin"));
            using var deactivated = await host.SendAsync(HttpMethod.Delete, $"/api/admin/users/{customer.Id}", administrator);
            Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
            await host.AssertAllRevokedAsync(customer);
            using var response = await host.RefreshAsync(session);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertRejectedAsync(response);
            await host.AssertLoginDeniedAsync(customer);
        }

        [Fact]
        public async Task IdentityPasswordLockout_DeniesRefreshAndRevokesThePresentedFamily()
        {
            await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString);
            var customer = await host.CreateUserAsync("User");
            var original = await host.LoginAsync(customer);
            var current = await host.RotateAsync(original);
            for (var attempt = 0; attempt < 5; attempt++) await host.AssertLoginDeniedAsync(customer, "Incorrect-password-123!");
            using var response = await host.RefreshAsync(current);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertRejectedAsync(response);
            await host.AssertAllRevokedAsync(customer);
            Assert.Equal(2, await host.TokenCountAsync(customer));
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public async Task RequiredConfirmation_AppliesToRefresh_ConfirmationDoesNotResurrectToken(bool requireAccount, bool requireEmail)
        {
            await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString, requireAccount: requireAccount, requireEmail: requireEmail);
            var customer = await host.CreateUserAsync("User");
            var session = await host.LoginAsync(customer);
            await host.SetConfirmationAsync(customer, false);
            using var response = await host.RefreshAsync(session);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertRejectedAsync(response);
            await host.AssertAllRevokedAsync(customer);
            var administrator = await host.LoginAsync(await host.CreateUserAsync("Admin"));
            using var confirmed = await host.AdminAsync(administrator, customer.Id, "confirm-email");
            Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
            using var stale = await host.RefreshAsync(session);
            Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
            await host.RotateAsync(await host.LoginAsync(customer));
        }

        [Fact]
        public async Task ConfirmationDisabled_UnconfirmedAccountCanLoginAndRefresh()
        {
            await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString, requireAccount: false, requireEmail: false);
            var customer = await host.CreateUserAsync("User", confirmed: false);
            await host.RotateAsync(await host.LoginAsync(customer));
        }

        [Theory]
        [InlineData("User", HttpStatusCode.Forbidden)]
        [InlineData("Admin", HttpStatusCode.OK)]
        public async Task NormalLoginAndRefresh_PreserveRolesAndLogout(string role, HttpStatusCode adminStatus)
        {
            await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString);
            var user = await host.CreateUserAsync(role);
            var session = await host.RotateAsync(await host.LoginAsync(user));
            using var customerResponse = await host.SendAsync(HttpMethod.Get, "/test/customer", session);
            Assert.Equal(HttpStatusCode.NoContent, customerResponse.StatusCode);
            using var adminResponse = await host.SendAsync(HttpMethod.Get, "/api/admin/users/roles", session);
            Assert.Equal(adminStatus, adminResponse.StatusCode);
            using var logout = await host.SendAsync(HttpMethod.Post, "/api/authentication/logout", session, useCookie: true);
            Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
            using var repeatedLogout = await host.SendAsync(HttpMethod.Post, "/api/authentication/logout", session, useCookie: true);
            Assert.Equal(HttpStatusCode.OK, repeatedLogout.StatusCode);
            using var stale = await host.RefreshAsync(session);
            Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
            await AssertRejectedAsync(stale);
        }

        [Fact]
        public async Task RotatedCookieReplay_RevokesDescendantsButNotIndependentSession()
        {
            await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString);
            var user = await host.CreateUserAsync("User");
            var original = await host.LoginAsync(user);
            var replacement = await host.RotateAsync(original);
            var independent = await host.LoginAsync(user);
            using var replay = await host.RefreshAsync(original);
            Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
            await AssertRejectedAsync(replay);
            using var descendant = await host.RefreshAsync(replacement);
            Assert.Equal(HttpStatusCode.BadRequest, descendant.StatusCode);
            await host.RotateAsync(independent);
        }

        [Fact]
        public async Task LockDoesNotIntroduceImmediateAccessJwtRevocation()
        {
            await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString);
            var user = await host.CreateUserAsync("User");
            var session = await host.LoginAsync(user);
            var admin = await host.LoginAsync(await host.CreateUserAsync("Admin"));
            using var locked = await host.AdminAsync(admin, user.Id, "lock", new UserLockRequestDto());
            Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
            using var existingAccess = await host.SendAsync(HttpMethod.Get, "/test/customer", session);
            Assert.Equal(HttpStatusCode.NoContent, existingAccess.StatusCode);
            using var denied = await host.RefreshAsync(session);
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        }

        [Fact]
        public async Task PasswordAndRoleChanges_PreserveExistingRefreshPolicyAndRefreshUsesCurrentRole()
        {
            await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString);
            var user = await host.CreateUserAsync("User");
            var session = await host.LoginAsync(user);
            var admin = await host.LoginAsync(await host.CreateUserAsync("Admin"));
            using var password = await host.SendAsync(HttpMethod.Post, "/api/authentication/change-password", session, new ChangePassword
            {
                CurrentPassword = IdentityHttpHost.Password,
                NewPassword = "Changed-isolated-password-456!",
                ConfirmPassword = "Changed-isolated-password-456!",
            });
            Assert.Equal(HttpStatusCode.OK, password.StatusCode);
            session = await host.RotateAsync(session);
            using var roles = await host.SendAsync(HttpMethod.Put, $"/api/admin/users/{user.Id}/roles", admin,
                new UpdateUserRolesDto { Roles = ["Admin"] });
            Assert.Equal(HttpStatusCode.OK, roles.StatusCode);
            using var oldRole = await host.SendAsync(HttpMethod.Get, "/api/admin/users/roles", session);
            Assert.Equal(HttpStatusCode.Forbidden, oldRole.StatusCode);
            var current = await host.RotateAsync(session);
            using var newRole = await host.SendAsync(HttpMethod.Get, "/api/admin/users/roles", current);
            Assert.Equal(HttpStatusCode.OK, newRole.StatusCode);
        }

        [Fact]
        public async Task LockBetweenEligibilityAndInsert_CannotIssueReplacement()
        {
            var barrier = new IssuanceBarrier();
            await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString, barrier);
            var user = await host.CreateUserAsync("User");
            var session = await host.LoginAsync(user);
            var admin = await host.LoginAsync(await host.CreateUserAsync("Admin"));
            barrier.Arm();
            var refresh = host.RefreshAsync(session);
            try
            {
                await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
                using var locked = await host.AdminAsync(admin, user.Id, "lock", new UserLockRequestDto());
                Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
            }
            finally { barrier.Release.TrySetResult(); }
            using var response = await refresh;
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertRejectedAsync(response);
            Assert.Equal(1, await host.TokenCountAsync(user));
            await host.AssertAllRevokedAsync(user);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task IssuanceAlreadyHoldingUserLock_CompletesBeforeAdminRevokesItsReplacement(bool deactivate)
        {
            var barrier = new IssuanceBarrier(pauseAfterLock: true);
            await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString, barrier);
            var user = await host.CreateUserAsync("User");
            var session = await host.LoginAsync(user);
            var admin = await host.LoginAsync(await host.CreateUserAsync("Admin"));
            barrier.Arm();
            var refresh = host.RotateAsync(session);
            Task<HttpResponseMessage>? locking = null;
            try
            {
                await barrier.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
                locking = deactivate
                    ? host.SendAsync(HttpMethod.Delete, $"/api/admin/users/{user.Id}", admin)
                    : host.AdminAsync(admin, user.Id, "lock", new UserLockRequestDto());
                await barrier.AccountWriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(30));
            }
            finally { barrier.Release.TrySetResult(); }

            var replacement = await refresh;
            using var locked = await locking!;
            Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
            await host.AssertAllRevokedAsync(user);
            using var denied = await host.RefreshAsync(replacement);
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
            await AssertRejectedAsync(denied);
        }

        private sealed class IssuanceBarrier(bool pauseAfterLock = false) : DbCommandInterceptor
        {
            private int armed;
            private DbCommand? issuanceCommand;
            public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource AccountWriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public void Arm() => Interlocked.Exchange(ref armed, 1);

            public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
                CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                if (command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal) && Interlocked.Exchange(ref armed, 0) == 1)
                {
                    issuanceCommand = command;
                    if (!pauseAfterLock) await PauseAsync(cancellationToken);
                }

                if (issuanceCommand is not null && command.CommandText.Contains("UPDATE \"AspNetUsers\"", StringComparison.Ordinal))
                    AccountWriteStarted.TrySetResult();

                return result;
            }

            public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
                DbDataReader result, CancellationToken cancellationToken = default)
            {
                if (pauseAfterLock && ReferenceEquals(issuanceCommand, command)) await PauseAsync(cancellationToken);
                return result;
            }

            private async Task PauseAsync(CancellationToken cancellationToken)
            {
                Reached.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }

        internal static async Task AssertRejectedAsync(HttpResponseMessage response)
        {
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.True(string.IsNullOrEmpty(result.Token));
            Assert.True(string.IsNullOrEmpty(result.RefreshToken));
            Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
            Assert.Contains(cookies, cookie => cookie.StartsWith("__Host-blazorshop-refresh=;", StringComparison.Ordinal)
                && cookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
        }

        internal sealed record TestUser(string Id, string Email);
        internal sealed record Session(string AccessToken, string Cookie);

        // Real application services, Identity stores, bearer validation and controllers.
        // TestServer replaces the socket; PostgreSQL is a disposable Testcontainers database.
        internal sealed class IdentityHttpHost(WebApplication app, HttpClient client) : IAsyncDisposable
        {
            public const string Password = "Isolated-test-password-123!";
            public IServiceProvider Services => app.Services;

            public static async Task<IdentityHttpHost> CreateAsync(string connectionString, IInterceptor? interceptor = null,
                bool requireAccount = true, bool requireEmail = true, string? observationKey = null, bool demoEnabled = false)
            {
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions
                {
                    EnvironmentName = "Development",
                    ContentRootPath = AppContext.BaseDirectory,
                });
                builder.WebHost.UseTestServer();
                builder.Logging.ClearProviders();
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString,
                    ["JWT:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
                    ["JWT:Issuer"] = "isolated-identity-tests",
                    ["JWT:Audience"] = "isolated-identity-tests",
                    ["ClientApp:BaseUrl"] = "https://localhost",
                    ["Commerce:Currency"] = "EUR",
                    ["Identity:RequireConfirmedAccount"] = requireAccount.ToString(),
                    ["Identity:RequireConfirmedEmail"] = requireEmail.ToString(),
                    ["EmailSettings:Enabled"] = "false",
                    ["Demo:Enabled"] = demoEnabled.ToString(),
                    ["Demo:Password"] = Password,
                    ["SessionObservation:Enabled"] = (observationKey is not null).ToString(),
                    ["SessionObservation:ServiceKey"] = observationKey,
                    ["Stripe:Enabled"] = "false",
                });
                builder.Services.AddApplication(builder.Configuration);
                builder.Services.AddInfrastructure(builder.Configuration);
                builder.Services.AddOptions<SessionObservationOptions>()
                    .Bind(builder.Configuration.GetSection(SessionObservationOptions.SectionName));
                if (interceptor is not null) builder.Services.AddDbContext<AppDbContext>(options => options.AddInterceptors(interceptor));
                builder.Services.AddOptions<IdentityConfirmationOptions>()
                    .Bind(builder.Configuration.GetSection(IdentityConfirmationOptions.SectionName));
                builder.Services.Configure<IdentityOptions>(options =>
                {
                    options.SignIn.RequireConfirmedAccount = requireAccount;
                    options.SignIn.RequireConfirmedEmail = requireEmail;
                });
                builder.Services.Configure<ApiRuntimeOptions>(_ => { });
                builder.Services.AddAuthorization();
                builder.Services.AddControllers().AddApplicationPart(typeof(AuthenticationController).Assembly);
                builder.Services.AddRateLimiter(options => options.AddPolicy("AuthApi",
                    _ => RateLimitPartition.GetNoLimiter("isolated-test")));
                var app = builder.Build();
                app.UseMiddleware<SessionObservationGate>();
                app.UseMiddleware<DemoSessionMiddleware>();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseRateLimiter();
                app.MapControllers();
                app.MapGet("/test/customer", () => Microsoft.AspNetCore.Http.Results.NoContent())
                    .RequireAuthorization(policy => policy.RequireRole("User", "Admin"));
                await app.StartAsync();
                var client = app.GetTestClient();
                client.BaseAddress = new Uri("https://localhost");
                return new IdentityHttpHost(app, client);
            }

            public async Task<TestUser> CreateUserAsync(string role, bool confirmed = true)
            {
                await using var scope = app.Services.CreateAsyncScope();
                var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                if (!await roles.RoleExistsAsync(role))
                {
                    Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
                }

                var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var email = $"identity-{Guid.NewGuid():N}@example.test";
                var user = new AppUser { UserName = email, Email = email, EmailConfirmed = confirmed, FullName = "Isolated User" };
                Assert.True((await users.CreateAsync(user, Password)).Succeeded);
                Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
                return new TestUser(user.Id, email);
            }

            public async Task<Session> LoginAsync(TestUser user, string? demoCredential = null)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/login")
                {
                    Content = JsonContent.Create(new LoginUser { Email = user.Email, Password = Password }),
                };
                if (demoCredential is not null) request.Headers.Add("X-BlazorShop-Demo-Session", demoCredential);
                using var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                return await ReadSessionAsync(response);
            }

            public async Task<Session> RotateAsync(Session session)
            {
                using var response = await RefreshAsync(session);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                return await ReadSessionAsync(response);
            }

            internal static async Task<Session> ReadSessionAsync(HttpResponseMessage response)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                Assert.NotNull(result);
                Assert.True(result.Success);
                Assert.True(!string.IsNullOrWhiteSpace(result.Token));
                Assert.True(string.IsNullOrEmpty(result.RefreshToken));
                var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-blazorshop-refresh=", StringComparison.Ordinal));
                return new Session(result.Token, cookie.Split(';')[0]);
            }

            public async Task AssertLoginDeniedAsync(TestUser user, string password = Password)
            {
                using var response = await client.PostAsJsonAsync("/api/authentication/login", new LoginUser { Email = user.Email, Password = password });
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                Assert.NotNull(result);
                Assert.False(result.Success);
                Assert.True(string.IsNullOrEmpty(result.Token));
                Assert.False(response.Headers.Contains("Set-Cookie"));
            }

            public async Task SetConfirmationAsync(TestUser user, bool confirmed)
            {
                await using var scope = app.Services.CreateAsyncScope();
                var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var stored = await users.FindByIdAsync(user.Id);
                Assert.NotNull(stored);
                stored.EmailConfirmed = confirmed;
                Assert.True((await users.UpdateAsync(stored)).Succeeded);
            }

            public async Task<int> TokenCountAsync(TestUser user)
            {
                await using var scope = app.Services.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<AppDbContext>().RefreshTokens.CountAsync(token => token.UserId == user.Id);
            }

            public async Task AssertAllRevokedAsync(TestUser user)
            {
                await using var scope = app.Services.CreateAsyncScope();
                var tokens = await scope.ServiceProvider.GetRequiredService<AppDbContext>().RefreshTokens
                    .AsNoTracking().Where(token => token.UserId == user.Id).ToListAsync();
                Assert.NotEmpty(tokens);
                Assert.All(tokens, token => Assert.NotNull(token.RevokedAtUtc));
            }

            public Task<HttpResponseMessage> RefreshAsync(Session session) => SendAsync(HttpMethod.Post, "/api/authentication/refresh-token", session, useCookie: true);

            public Task<HttpResponseMessage> AdminAsync(Session admin, string id, string action, UserLockRequestDto? request = null) =>
                SendAsync(HttpMethod.Post, $"/api/admin/users/{id}/{action}", admin, request);

            public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, Session session, object? body = null, bool useCookie = false)
            {
                using var request = new HttpRequestMessage(method, path);
                if (useCookie) request.Headers.Add("Cookie", session.Cookie);
                else request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
                if (body is not null) request.Content = JsonContent.Create(body);
                return await client.SendAsync(request);
            }

            public Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, CancellationToken cancellationToken = default) => client.SendAsync(request, cancellationToken);

            public async ValueTask DisposeAsync()
            {
                client.Dispose();
                await app.DisposeAsync();
            }
        }
    }
}
