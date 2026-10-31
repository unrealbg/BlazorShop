namespace BlazorShop.Tests.Infrastructure.PostgreSql
{
    using System.Net;
    using System.Net.Http.Json;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Users;
    using BlazorShop.Application.DTOs.Demo;
    using BlazorShop.Application.Options;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Demo;
    using BlazorShop.Storefront.Services;
    using BlazorShop.Storefront.Services.Contracts;

    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    using Xunit;

    using static BlazorShop.Tests.Infrastructure.PostgreSql.RefreshEligibilityHttpTests;

    [Collection(PostgreSqlCollection.Name)]
    public sealed class SessionObservationHttpTests(PostgreSqlFixture database)
    {
        private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        [Theory]
        [InlineData("User")]
        [InlineData("Admin")]
        public async Task RepeatedAndConcurrentSsrGets_DoNotRotateAndKeepPrivateSeo(string role)
        {
            var key = NewKey();
            await using var api = await IdentityHttpHost.CreateAsync(database.ConnectionString, observationKey: key);
            var user = await api.CreateUserAsync(role);
            var session = await api.LoginAsync(user);
            var before = await SnapshotAsync(api, user.Id);
            var bridge = new BridgeCounters();
            await using var storefront = CreateStorefront(api, key, bridge);
            using var browser = storefront.CreateClient();
            for (var i = 0; i < 3; i++) await GetPageAsync(browser, session.Cookie, "/faq", user.Email);
            await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => GetPageAsync(browser, session.Cookie, "/faq", user.Email)));
            Assert.Equal(11, bridge.Observations);
            Assert.Equal(0, bridge.Refreshes);
            Assert.True(before.AsEnumerable().SequenceEqual(await SnapshotAsync(api, user.Id)));

            using var observation = await ObserveAsync(api, session, key);
            Assert.Equal(HttpStatusCode.OK, observation.StatusCode);
            using var json = JsonDocument.Parse(await observation.Content.ReadAsStringAsync());
            Assert.Equal(new[] { "displayName", "email", "isAdmin", "isAuthenticated" }, json.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray());
            Assert.True(json.RootElement.GetProperty("isAuthenticated").GetBoolean());
            Assert.Equal(role == "Admin", json.RootElement.GetProperty("isAdmin").GetBoolean());
            Assert.False(observation.Headers.Contains("Set-Cookie"));
            await api.RotateAsync(session);
        }

        [Theory]
        [InlineData("missing")]
        [InlineData("invalid")]
        [InlineData("expired")]
        [InlineData("revoked")]
        [InlineData("unconfirmed")]
        [InlineData("locked")]
        public async Task UnavailableCredentials_AreAnonymousWithoutChangingTokensOrCookies(string state)
        {
            var key = NewKey();
            await using var api = await IdentityHttpHost.CreateAsync(database.ConnectionString, observationKey: key);
            var user = await api.CreateUserAsync("User");
            var session = await api.LoginAsync(user);
            await using (var scope = api.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var token = await db.RefreshTokens.SingleAsync(token => token.UserId == user.Id);
                if (state == "expired") token.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
                if (state == "revoked") token.RevokedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();
                if (state == "locked")
                {
                    var users = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<BlazorShop.Domain.Entities.Identity.AppUser>>();
                    var account = await users.FindByIdAsync(user.Id);
                    Assert.NotNull(account);
                    Assert.True((await users.SetLockoutEndDateAsync(account, DateTimeOffset.UtcNow.AddHours(1))).Succeeded);
                }
            }
            if (state == "unconfirmed") await api.SetConfirmationAsync(user, false);
            var before = await SnapshotAsync(api, user.Id);
            var credential = state == "missing" ? null : state == "invalid" ? session with { Cookie = "__Host-blazorshop-refresh=invalid" } : session;
            using var response = await ObserveAsync(api, credential, key);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var observed = await response.Content.ReadFromJsonAsync<SessionObservation>();
            Assert.Equal(SessionObservation.Anonymous, observed);
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.True(before.AsEnumerable().SequenceEqual(await SnapshotAsync(api, user.Id)));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task LockedOrDeactivatedAccount_IsAnonymousAndObservationDoesNotWrite(bool deactivate)
        {
            var key = NewKey();
            await using var api = await IdentityHttpHost.CreateAsync(database.ConnectionString, observationKey: key);
            var user = await api.CreateUserAsync("User");
            var session = await api.LoginAsync(user);
            var admin = await api.LoginAsync(await api.CreateUserAsync("Admin"));
            using var restriction = deactivate
                ? await api.SendAsync(HttpMethod.Delete, $"/api/admin/users/{user.Id}", admin)
                : await api.AdminAsync(admin, user.Id, "lock", new UserLockRequestDto());
            Assert.Equal(HttpStatusCode.OK, restriction.StatusCode);
            await api.AssertAllRevokedAsync(user);
            var before = await SnapshotAsync(api, user.Id);
            using var response = await ObserveAsync(api, session, key);
            Assert.Equal(SessionObservation.Anonymous, await response.Content.ReadFromJsonAsync<SessionObservation>());
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.True(before.AsEnumerable().SequenceEqual(await SnapshotAsync(api, user.Id)));
        }

        [Fact]
        public async Task ObservingRotatedCookie_DoesNotTriggerReplayRevocation()
        {
            var key = NewKey();
            await using var api = await IdentityHttpHost.CreateAsync(database.ConnectionString, observationKey: key);
            var user = await api.CreateUserAsync("User");
            var original = await api.LoginAsync(user);
            var replacement = await api.RotateAsync(original);
            var before = await SnapshotAsync(api, user.Id);
            using var response = await ObserveAsync(api, original, key);
            Assert.Equal(SessionObservation.Anonymous, await response.Content.ReadFromJsonAsync<SessionObservation>());
            Assert.True(before.AsEnumerable().SequenceEqual(await SnapshotAsync(api, user.Id)));
            await api.RotateAsync(replacement);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("incorrect")]
        public async Task UntrustedCaller_IsRejectedBeforeDemoInspection(string? suppliedKey)
        {
            await using var api = await IdentityHttpHost.CreateAsync(database.ConnectionString, observationKey: NewKey(), demoEnabled: true);
            using var response = await ObserveAsync(api, null, suppliedKey, demo: "unknown-demo-session");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }

        [Fact]
        public async Task WrongWellFormedServiceKey_CannotObserveKnownSession()
        {
            await using var api = await IdentityHttpHost.CreateAsync(database.ConnectionString, observationKey: NewKey());
            var session = await api.LoginAsync(await api.CreateUserAsync("User"));
            using var response = await ObserveAsync(api, session, NewKey());
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.False(response.Headers.Contains("Set-Cookie"));
        }

        [Fact]
        public async Task ServiceCredential_DoesNotAuthorizeBusinessEndpoints_AndDisabledFeatureIsClosed()
        {
            var key = NewKey();
            await using var api = await IdentityHttpHost.CreateAsync(database.ConnectionString, observationKey: key);
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/users/roles");
            request.Headers.Add(SessionObservationOptions.CredentialHeader, key);
            using var response = await api.SendRawAsync(request);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            await using var disabled = await IdentityHttpHost.CreateAsync(database.ConnectionString);
            using var absent = await ObserveAsync(disabled, null, key);
            Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        }

        [Theory]
        [InlineData("User")]
        [InlineData("Admin")]
        public async Task DemoObservation_RequiresMatchingActiveContextAndCannotRebindNormalSession(string role)
        {
            var key = NewKey();
            await using var api = await IdentityHttpHost.CreateAsync(database.ConnectionString, observationKey: key, demoEnabled: true);
            var normal = await api.CreateUserAsync("User");
            await api.CreateUserAsync("Admin");
            var normalSession = await api.LoginAsync(normal);
            var (demoCredential, demoUser) = await StartDemoAsync(api, role);
            var demoSession = await api.LoginAsync(demoUser, demoCredential);
            var deadline = await DemoDeadlineAsync(api, demoCredential);
            using var active = await ObserveAsync(api, demoSession, key, demoCredential);
            var activeState = await active.Content.ReadFromJsonAsync<SessionObservation>();
            Assert.NotNull(activeState);
            Assert.True(activeState.IsAuthenticated);
            Assert.Equal(role == "Admin", activeState.IsAdmin);
            Assert.False(active.Headers.Contains("Set-Cookie"));
            Assert.Equal(deadline, await DemoDeadlineAsync(api, demoCredential));
            await using (var ordinary = await api.Services.GetRequiredService<IDemoSessionManager>().AcquireAsync(demoCredential))
            {
                Assert.NotNull(ordinary);
                Assert.True(ordinary.Context.ExpiresAtUtc > deadline);
            }
            using var missing = await ObserveAsync(api, demoSession, key);
            Assert.Equal(SessionObservation.Anonymous, await missing.Content.ReadFromJsonAsync<SessionObservation>());
            using var rebound = await ObserveAsync(api, normalSession, key, demoCredential);
            Assert.Equal(SessionObservation.Anonymous, await rebound.Content.ReadFromJsonAsync<SessionObservation>());
            var (otherContext, _) = await StartDemoAsync(api, role);
            using var mismatched = await ObserveAsync(api, demoSession, key, otherContext);
            Assert.Equal(SessionObservation.Anonymous, await mismatched.Content.ReadFromJsonAsync<SessionObservation>());
            await api.Services.GetRequiredService<IDemoSessionManager>().EndAsync(demoCredential);
            using var ended = await ObserveAsync(api, demoSession, key, demoCredential);
            Assert.Equal(HttpStatusCode.Gone, ended.StatusCode);
            Assert.False(ended.Headers.Contains("Set-Cookie"));
        }

        [Fact]
        public async Task DifferentUsers_ShareClientPipelineWithoutSharingIdentity_AndCheckoutKeepsItsRoute()
        {
            var key = NewKey();
            await using var api = await IdentityHttpHost.CreateAsync(database.ConnectionString, observationKey: key);
            var firstUser = await api.CreateUserAsync("User");
            var secondUser = await api.CreateUserAsync("Admin");
            var first = await api.LoginAsync(firstUser);
            var second = await api.LoginAsync(secondUser);
            await using var storefront = CreateStorefront(api, key, new BridgeCounters());
            using var client = storefront.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            await Task.WhenAll(Enumerable.Range(0, 8).Select(i => GetPageAsync(client,
                i % 2 == 0 ? first.Cookie : second.Cookie, "/faq", i % 2 == 0 ? firstUser.Email : secondUser.Email,
                i % 2 == 0 ? secondUser.Email : firstUser.Email)));
            using var checkout = new HttpRequestMessage(HttpMethod.Get, "/checkout");
            checkout.Headers.Add("Cookie", first.Cookie);
            using var response = await client.SendAsync(checkout);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("https://account.example.test/account/checkout", response.Headers.Location?.AbsoluteUri);
            Assert.True(response.Headers.CacheControl?.Private);
            Assert.True(response.Headers.CacheControl?.NoStore);
            using var anonymous = await client.GetAsync("/checkout");
            Assert.Equal("https://account.example.test/authentication/login/account/checkout", anonymous.Headers.Location?.AbsoluteUri);
        }

        private static async Task GetPageAsync(HttpClient client, string cookie, string path, string expectedEmail, string? excludedEmail = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Cookie", cookie);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.Private);
            Assert.True(response.Headers.CacheControl?.NoStore);
            Assert.False(response.Headers.Contains("Set-Cookie"));
            Assert.False(response.Headers.TryGetValues("X-Robots-Tag", out var robots) && robots.Any(value => value.Contains("noindex", StringComparison.OrdinalIgnoreCase)));
            var html = await response.Content.ReadAsStringAsync();
            Assert.Contains(expectedEmail, html);
            if (excludedEmail is not null) Assert.DoesNotContain(excludedEmail, html);
            Assert.Contains("rel=\"canonical\"", html);
            Assert.DoesNotContain("name=\"robots\" content=\"noindex", html);
        }

        private static Task<HttpResponseMessage> ObserveAsync(IdentityHttpHost host, Session? session, string? key, string? demo = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/internal/storefront/session");
            if (session is not null) request.Headers.Add("Cookie", session.Cookie);
            if (key is not null) request.Headers.Add(SessionObservationOptions.CredentialHeader, key);
            if (demo is not null) request.Headers.Add("X-BlazorShop-Demo-Session", demo);
            return SendAndDisposeAsync(host, request);
        }

        private static async Task<HttpResponseMessage> SendAndDisposeAsync(IdentityHttpHost host, HttpRequestMessage request)
        {
            using (request) return await host.SendRawAsync(request);
        }

        private static async Task<byte[]> SnapshotAsync(IdentityHttpHost host, string userId)
        {
            await using var scope = host.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tokens = await db.RefreshTokens.AsNoTracking().Where(token => token.UserId == userId).OrderBy(token => token.Id).ToListAsync();
            var user = await db.Users.AsNoTracking().SingleAsync(user => user.Id == userId);
            return SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { tokens, user })));
        }

        private static async Task<(string Credential, TestUser User)> StartDemoAsync(IdentityHttpHost host, string role)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/demo/sessions") { Content = JsonContent.Create(new StartDemoSessionRequest { Role = role }) };
            using var response = await host.SendRawAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var model = await response.Content.ReadFromJsonAsync<DemoSessionResponse>();
            Assert.NotNull(model);
            Assert.True(model.Success);
            var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("blazorshop-demo-session=", StringComparison.Ordinal)).Split(';')[0];
            return (Uri.UnescapeDataString(cookie[(cookie.IndexOf('=') + 1)..]), new TestUser("unused", model.Email));
        }

        private static async Task<DateTime> DemoDeadlineAsync(IdentityHttpHost host, string credential)
        {
            await using var lease = await host.Services.GetRequiredService<IDemoSessionManager>().AcquireAsync(credential, readOnly: true);
            Assert.NotNull(lease);
            return lease.Context.ExpiresAtUtc;
        }

        private static WebApplicationFactory<global::Program> CreateStorefront(IdentityHttpHost api, string key, BridgeCounters counters) =>
            new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Api:BaseUrl"] = "https://localhost/api/",
                    ["ClientApp:BaseUrl"] = "https://account.example.test/",
                    ["PublicUrl:BaseUrl"] = "https://shop.example.test/",
                    ["SessionObservation:Enabled"] = "true",
                    ["SessionObservation:ServiceKey"] = key,
                }));
                builder.ConfigureTestServices(services =>
                {
                    services.AddHttpClient<IStorefrontSessionResolver, StorefrontSessionResolver>().ConfigurePrimaryHttpMessageHandler(() => new ApiBridge(api, counters));
                    services.AddHttpClient<StorefrontApiClient>().ConfigurePrimaryHttpMessageHandler(() => new ApiBridge(api, counters));
                });
            });

        private sealed class BridgeCounters { public int Observations; public int Refreshes; }
        private sealed class ApiBridge(IdentityHttpHost api, BridgeCounters counters) : HttpMessageHandler
        {
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (request.RequestUri!.AbsolutePath.EndsWith("/internal/storefront/session", StringComparison.Ordinal)) Interlocked.Increment(ref counters.Observations);
                if (request.RequestUri.AbsolutePath.EndsWith("/refresh-token", StringComparison.Ordinal)) Interlocked.Increment(ref counters.Refreshes);
                using var forwarded = new HttpRequestMessage(request.Method, request.RequestUri);
                foreach (var header in request.Headers) forwarded.Headers.TryAddWithoutValidation(header.Key, header.Value);
                return await api.SendRawAsync(forwarded);
            }
        }
    }
}
