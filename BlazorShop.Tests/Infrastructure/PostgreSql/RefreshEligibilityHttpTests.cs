namespace BlazorShop.Tests.Infrastructure.PostgreSql
{
    using System.Net;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Security.Cryptography;
    using System.Threading.RateLimiting;

    using BlazorShop.API.Controllers;
    using BlazorShop.API.Demo;
    using BlazorShop.API.Options;
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
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    using Xunit;

    [Collection(PostgreSqlCollection.Name)]
    public sealed class RefreshEligibilityHttpTests(PostgreSqlFixture database)
    {
        [Fact]
        public async Task AdminLock_RejectsExistingRefreshCookieAndClearsIt()
        {
            await using var host = await IdentityHttpHost.CreateAsync(database.ConnectionString);
            var customer = await host.CreateUserAsync("User");
            var admin = await host.CreateUserAsync("Admin");
            var session = await host.LoginAsync(customer);
            var administrator = await host.LoginAsync(admin);

            using var locked = await host.AdminAsync(administrator, customer.Id, "lock", new UserLockRequestDto());
            Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
            using var response = await host.RefreshAsync(session);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await AssertRejectedAsync(response);
        }

        private static async Task AssertRejectedAsync(HttpResponseMessage response)
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

        private sealed record TestUser(string Id, string Email);
        private sealed record Session(string AccessToken, string Cookie);

        // Real application services, Identity stores, bearer validation and controllers.
        // TestServer replaces the socket; PostgreSQL is a disposable Testcontainers database.
        private sealed class IdentityHttpHost(WebApplication app, HttpClient client) : IAsyncDisposable
        {
            private const string Password = "Isolated-test-password-123!";

            public static async Task<IdentityHttpHost> CreateAsync(string connectionString)
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
                    ["Identity:RequireConfirmedAccount"] = "true",
                    ["Identity:RequireConfirmedEmail"] = "true",
                    ["EmailSettings:Enabled"] = "false",
                    ["Demo:Enabled"] = "false",
                    ["Stripe:Enabled"] = "false",
                });
                builder.Services.AddApplication(builder.Configuration);
                builder.Services.AddInfrastructure(builder.Configuration);
                builder.Services.AddOptions<IdentityConfirmationOptions>()
                    .Bind(builder.Configuration.GetSection(IdentityConfirmationOptions.SectionName));
                builder.Services.Configure<IdentityOptions>(options =>
                {
                    options.SignIn.RequireConfirmedAccount = true;
                    options.SignIn.RequireConfirmedEmail = true;
                });
                builder.Services.Configure<ApiRuntimeOptions>(_ => { });
                builder.Services.AddAuthorization();
                builder.Services.AddControllers().AddApplicationPart(typeof(AuthenticationController).Assembly);
                builder.Services.AddRateLimiter(options => options.AddPolicy("AuthApi",
                    _ => RateLimitPartition.GetNoLimiter("isolated-test")));
                var app = builder.Build();
                app.UseMiddleware<DemoSessionMiddleware>();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseRateLimiter();
                app.MapControllers();
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

            public async Task<Session> LoginAsync(TestUser user)
            {
                using var response = await client.PostAsJsonAsync("/api/authentication/login", new LoginUser { Email = user.Email, Password = Password });
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                Assert.NotNull(result);
                Assert.True(result.Success);
                Assert.True(!string.IsNullOrWhiteSpace(result.Token));
                Assert.True(string.IsNullOrEmpty(result.RefreshToken));
                var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-blazorshop-refresh=", StringComparison.Ordinal));
                return new Session(result.Token, cookie.Split(';')[0]);
            }

            public Task<HttpResponseMessage> RefreshAsync(Session session) => SendAsync(HttpMethod.Post, "/api/authentication/refresh-token", session, useCookie: true);

            public Task<HttpResponseMessage> AdminAsync(Session admin, string id, string action, UserLockRequestDto? request = null) =>
                SendAsync(HttpMethod.Post, $"/api/admin/users/{id}/{action}", admin, request);

            private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, Session session, object? body = null, bool useCookie = false)
            {
                using var request = new HttpRequestMessage(method, path);
                if (useCookie) request.Headers.Add("Cookie", session.Cookie);
                else request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
                if (body is not null) request.Content = JsonContent.Create(body);
                return await client.SendAsync(request);
            }

            public async ValueTask DisposeAsync()
            {
                client.Dispose();
                await app.DisposeAsync();
            }
        }
    }
}
