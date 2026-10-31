namespace BlazorShop.Tests.Presentation.Authentication
{
    using System.IdentityModel.Tokens.Jwt;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Security.Claims;
    using System.Security.Cryptography;
    using System.Text;

    using BlazorShop.API.Demo;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts.Demo;
    using BlazorShop.Infrastructure;
    using BlazorShop.Infrastructure.Demo;
    using BlazorShop.Infrastructure.Repositories.Authentication;

    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.TestHost;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.IdentityModel.Tokens;

    using Npgsql;

    using Xunit;

    public class DemoJwtIsolationTests
    {
        [Theory]
        [InlineData(false, "Admin")]
        [InlineData(false, "User")]
        [InlineData(true, "Admin")]
        [InlineData(true, "User")]
        public async Task DemoToken_WithoutSessionCannotAuthenticate(bool enabled, string role)
        {
            await using var host = await BoundaryHost.CreateAsync(enabled);
            using var response = await host.SendAsync(host.Token(role, ["session-a"]));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, host.EndpointCalls);
        }

        [Fact]
        public async Task PreviouslyIssuedDemoToken_IsRejectedAfterDemoIsDisabled()
        {
            await using var host = await BoundaryHost.CreateAsync(true);
            var token = host.Token("Admin", ["session-a"]);
            using var active = await host.SendAsync(token, "active-a", admin: true);
            Assert.Equal(HttpStatusCode.NoContent, active.StatusCode);
            host.DemoOptions.Enabled = false;
            using var disabled = await host.SendAsync(token, admin: true);
            Assert.Equal(HttpStatusCode.Unauthorized, disabled.StatusCode);
            Assert.Equal(1, host.EndpointCalls);
        }

        [Theory]
        [InlineData("cookie")]
        [InlineData("header")]
        [InlineData("both")]
        public async Task MatchingActiveSession_AllowsDemoAdmin(string transport)
        {
            await using var host = await BoundaryHost.CreateAsync(true);
            using var response = await host.SendAsync(host.Token("Admin", ["session-a"]),
                transport == "header" ? null : "active-a", transport == "cookie" ? null : "active-a", admin: true);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal("demo", Assert.Single(response.Headers.GetValues("X-Test-Context")));
            Assert.Equal(1, host.Manager.ReleaseCalls);
        }

        [Theory]
        [InlineData("session-b", "active-a", 401)]
        [InlineData("session-a", "missing", 410)]
        [InlineData("session-a", "ended", 410)]
        [InlineData("session-a", "expired", 401)]
        [InlineData("", "active-a", 401)]
        [InlineData(" ", "active-a", 401)]
        public async Task InvalidOrUnavailableSession_DoesNotReachEndpoint(string sessionId, string cookie, int expected)
        {
            await using var host = await BoundaryHost.CreateAsync(true);
            using var response = await host.SendAsync(host.Token("Admin", [sessionId]), cookie, admin: true);
            Assert.Equal((HttpStatusCode)expected, response.StatusCode);
            Assert.Equal(0, host.EndpointCalls);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task DuplicateDemoClaims_AreRejected(bool enabled)
        {
            await using var host = await BoundaryHost.CreateAsync(enabled);
            using var response = await host.SendAsync(host.Token("Admin", ["session-a", "session-a"]), "active-a");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, host.EndpointCalls);
        }

        [Theory]
        [InlineData("session-a")]
        [InlineData("")]
        public async Task DisabledDemoToken_CannotReachAnonymousEndpoint(string sessionId)
        {
            await using var host = await BoundaryHost.CreateAsync(false);
            using var response = await host.SendAsync(host.Token("Admin", [sessionId]), path: "/public");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, host.EndpointCalls);
        }

        [Fact]
        public async Task MatchingDemoCustomer_PreservesRoleBoundary()
        {
            await using var host = await BoundaryHost.CreateAsync(true);
            var token = host.Token("User", ["session-a"]);
            using var customer = await host.SendAsync(token, "active-a");
            Assert.Equal(HttpStatusCode.NoContent, customer.StatusCode);
            using var admin = await host.SendAsync(token, "active-a", admin: true);
            Assert.Equal(HttpStatusCode.Forbidden, admin.StatusCode);
        }

        [Theory]
        [InlineData(false, "session-a")]
        [InlineData(true, "session-a")]
        [InlineData(true, "")]
        public async Task BearerValidation_RejectsDemoTokenWithoutActivatedContext(bool enabled, string sessionId)
        {
            await using var host = await BoundaryHost.CreateAsync(enabled, useDemoMiddleware: false);
            using var response = await host.SendAsync(host.Token("Admin", [sessionId]));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, host.EndpointCalls);
        }

        [Fact]
        public async Task CookieAndHeaderMismatch_IsRejected()
        {
            await using var host = await BoundaryHost.CreateAsync(true);
            using var response = await host.SendAsync(host.Token("Admin", ["session-a"]), "active-a", "active-b");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, host.EndpointCalls);
        }

        [Theory]
        [InlineData(false, "Admin")]
        [InlineData(false, "User")]
        [InlineData(true, "Admin")]
        [InlineData(true, "User")]
        public async Task OrdinaryTokens_WorkOutsideDemoContext(bool enabled, string role)
        {
            await using var host = await BoundaryHost.CreateAsync(enabled);
            using var customer = await host.SendAsync(host.Token(role));
            Assert.Equal(HttpStatusCode.NoContent, customer.StatusCode);
            Assert.Equal("normal", Assert.Single(customer.Headers.GetValues("X-Test-Context")));
            using var admin = await host.SendAsync(host.Token(role), admin: true);
            Assert.Equal(role == "Admin" ? HttpStatusCode.NoContent : HttpStatusCode.Forbidden, admin.StatusCode);
        }

        [Theory]
        [InlineData("Admin")]
        [InlineData("User")]
        public async Task OrdinaryTokens_CannotBeReboundToAnActiveDemoSession(string role)
        {
            await using var host = await BoundaryHost.CreateAsync(true);
            using var response = await host.SendAsync(host.Token(role), "active-a");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, host.EndpointCalls);
        }

        [Theory]
        [InlineData("signature")]
        [InlineData("issuer")]
        [InlineData("audience")]
        [InlineData("expiry")]
        public async Task StandardJwtValidation_IsStillRequired(string fault)
        {
            await using var host = await BoundaryHost.CreateAsync(true);
            using var response = await host.SendAsync(host.Token("Admin", ["session-a"], fault), "active-a");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(0, host.EndpointCalls);
        }

        [Fact]
        public async Task AnonymousDemoRequests_AndSubsequentNormalRequestsRemainIsolated()
        {
            await using var host = await BoundaryHost.CreateAsync(true);
            using var anonymous = await host.SendAsync(null);
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            using var demoPublic = await host.SendAsync(null, "active-a", path: "/public");
            Assert.Equal(HttpStatusCode.NoContent, demoPublic.StatusCode);
            Assert.Equal("demo", Assert.Single(demoPublic.Headers.GetValues("X-Test-Context")));
            using var first = await host.SendAsync(host.Token("Admin", ["session-a"]), "active-a");
            Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
            using var normal = await host.SendAsync(host.Token("Admin"));
            Assert.Equal(HttpStatusCode.NoContent, normal.StatusCode);
            Assert.Equal("normal", Assert.Single(normal.Headers.GetValues("X-Test-Context")));
            using var second = await host.SendAsync(host.Token("Admin", ["session-b"]), "active-b");
            Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
            Assert.Equal("demo", Assert.Single(second.Headers.GetValues("X-Test-Context")));
        }

        private sealed class BoundaryHost : IAsyncDisposable
        {
            private readonly WebApplication _app;
            private HttpClient _client = null!;
            private readonly IConfiguration _configuration;

            private BoundaryHost(WebApplication app, IConfiguration configuration, DemoOptions options, Sessions manager)
            {
                _app = app;
                _configuration = configuration;
                DemoOptions = options;
                Manager = manager;
            }

            public DemoOptions DemoOptions { get; }
            public Sessions Manager { get; }
            public int EndpointCalls { get; private set; }

            public static async Task<BoundaryHost> CreateAsync(bool enabled, bool useDemoMiddleware = true)
            {
                var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["JWT:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
                    ["JWT:Issuer"] = "test-issuer",
                    ["JWT:Audience"] = "test-audience",
                }).Build();
                var registrations = new ServiceCollection();
                registrations.AddLogging();
                registrations.AddSingleton<IConfiguration>(configuration);
                registrations.AddInfrastructure(configuration);
                await using var provider = registrations.BuildServiceProvider();
                var actual = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
                var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
                builder.WebHost.UseTestServer();
                builder.Logging.ClearProviders();
                var options = new DemoOptions { Enabled = enabled };
                var context = new DemoRequestContext();
                var manager = new Sessions(enabled);
                builder.Services.AddSingleton<IOptions<DemoOptions>>(Options.Create(options));
                builder.Services.AddSingleton(context);
                builder.Services.AddSingleton<IDemoRequestContext>(context);
                builder.Services.AddSingleton<IDemoSessionManager>(manager);
                builder.Services.AddAuthorization();
                builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(bearer =>
                {
                    bearer.TokenValidationParameters = actual.TokenValidationParameters.Clone();
                    bearer.Events = actual.Events;
                    bearer.MapInboundClaims = actual.MapInboundClaims;
                });
                var app = builder.Build();
                var host = new BoundaryHost(app, configuration, options, manager);
                if (useDemoMiddleware) app.UseMiddleware<DemoSessionMiddleware>();
                app.UseAuthentication();
                app.UseAuthorization();
                Task Respond(HttpContext http)
                {
                    host.EndpointCalls++;
                    http.Response.Headers["X-Test-Context"] = context.IsDemo ? "demo" : "normal";
                    http.Response.StatusCode = StatusCodes.Status204NoContent;
                    return Task.CompletedTask;
                }

                app.MapGet("/protected", Respond).RequireAuthorization(policy => policy.RequireRole("User", "Admin"));
                app.MapGet("/admin", Respond).RequireAuthorization(policy => policy.RequireRole("Admin"));
                app.MapGet("/public", Respond).AllowAnonymous();
                await app.StartAsync();
                host._client = app.GetTestClient();
                return host;
            }

            public string Token(string role, string[]? demoIds = null, string? fault = null)
            {
                var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new(ClaimTypes.Role, role) };
                claims.AddRange((demoIds ?? []).Select(id => new Claim(DemoSessionConstants.ClaimType, id)));
                if (fault is null)
                {
                    return new AppTokenManager(null!, _configuration, null!).GenerateAccessToken(claims);
                }

                var key = fault == "signature" ? RandomNumberGenerator.GetBytes(48) : Encoding.UTF8.GetBytes(_configuration["JWT:Key"]!);
                var token = new JwtSecurityToken(fault == "issuer" ? "wrong" : _configuration["JWT:Issuer"],
                    fault == "audience" ? "wrong" : _configuration["JWT:Audience"], claims,
                    expires: fault == "expiry" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
                    signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));
                return new JwtSecurityTokenHandler().WriteToken(token);
            }

            public async Task<HttpResponseMessage> SendAsync(string? token, string? cookie = null, string? header = null, bool admin = false, string? path = null)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, path ?? (admin ? "/admin" : "/protected"));
                if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (cookie is not null) request.Headers.Add("Cookie", $"{DemoOptions.CookieName}={cookie}");
                if (header is not null) request.Headers.Add(DemoOptions.HeaderName, header);
                return await _client.SendAsync(request);
            }

            public async ValueTask DisposeAsync()
            {
                _client.Dispose();
                await _app.DisposeAsync();
                await Manager.DisposeAsync();
            }
        }

        private sealed class Sessions(bool enabled) : IDemoSessionManager, IAsyncDisposable
        {
            private readonly NpgsqlConnection _connection = new();
            public bool IsEnabled => enabled;
            public int ReleaseCalls { get; private set; }
            public Task<DemoSessionStartResult> CreateAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public ValueTask<DemoSessionLease?> AcquireAsync(string token, CancellationToken cancellationToken = default)
            {
                if (token is not ("active-a" or "active-b" or "expired")) return ValueTask.FromResult<DemoSessionLease?>(null);
                var sessionId = token == "active-b" ? "session-b" : "session-a";
                var expiry = token == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(30);
                var context = (DemoSessionContext)Activator.CreateInstance(typeof(DemoSessionContext), BindingFlags.Instance | BindingFlags.NonPublic,
                    null, [sessionId, token, _connection, expiry], null)!;
                var lease = (DemoSessionLease)Activator.CreateInstance(typeof(DemoSessionLease), BindingFlags.Instance | BindingFlags.NonPublic,
                    null, [context, (Action)(() => ReleaseCalls++)], null)!;
                return ValueTask.FromResult<DemoSessionLease?>(lease);
            }

            public Task EndAsync(string token, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task CleanupExpiredAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
            public void RegisterUploadedFile(string sessionId, string filePath) => throw new NotSupportedException();
            public ValueTask DisposeAsync() => _connection.DisposeAsync();
        }
    }
}
