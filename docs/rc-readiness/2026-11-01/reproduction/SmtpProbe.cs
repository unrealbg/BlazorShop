namespace BlazorShop.RcAudit;

using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using BlazorShop.API.Controllers;
using BlazorShop.API.Options;
using BlazorShop.Application;
using BlazorShop.Application.DTOs;
using BlazorShop.Application.DTOs.Payment;
using BlazorShop.Application.DTOs.UserIdentity;
using BlazorShop.Application.Options;
using BlazorShop.Domain.Entities.Identity;
using BlazorShop.Domain.Entities;
using BlazorShop.Infrastructure;
using BlazorShop.Infrastructure.Data;
using BlazorShop.Tests.Infrastructure.PostgreSql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Xunit;

public sealed class SmtpProbe
{
    [Fact]
    public async Task RealIdentityHttpAndLoopbackSmtp_ObserveCommitBoundaries()
    {
        await using var database = new DatabaseScope();
        await database.InitializeAsync();
        var results = new List<object>();
        foreach (var mode in new[] { "healthy", "closed-port", "accepted-data-lost-ack" })
        {
            await using var sink = new SmtpSink(mode);
            await using var app = await CreateHostAsync(database.Fixture.ConnectionString, sink.Port);
            using var http = app.GetTestClient();
            http.BaseAddress = new Uri("https://localhost");
            var email = $"audit-{Guid.NewGuid():N}@example.test";
            var password = $"Aa1!{Guid.NewGuid():N}";
            using var response = await http.PostAsJsonAsync("/api/authentication/create", new CreateUser
            {
                Email = email,
                FullName = "Synthetic Audit Customer",
                Password = password,
                ConfirmPassword = password,
            });
            await using var scope = app.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Email == email);
            int? confirmationStatus = null;
            int? loginStatus = null;
            var accessIssued = false;
            var cookieSecure = false;
            var cookieHttpOnly = false;
            var cookieLax = false;
            if (sink.Messages.Count > 0)
            {
                using var content = new MemoryStream(Encoding.UTF8.GetBytes(sink.Messages[0]));
                var mime = await MimeMessage.LoadAsync(content);
                Assert.NotNull(mime.HtmlBody);
                var link = Regex.Match(mime.HtmlBody, "href=\"([^\"]+)\"").Groups[1].Value;
                var uri = new Uri(WebUtility.HtmlDecode(link));
                using var confirm = await http.GetAsync("/api/authentication/confirm-email" + uri.Query);
                confirmationStatus = (int)confirm.StatusCode;
            }
            if (mode == "healthy")
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                Assert.NotNull(user);
                Assert.Equal(200, confirmationStatus);
                using var login = await http.PostAsJsonAsync("/api/authentication/login", new LoginUser { Email = email, Password = password });
                loginStatus = (int)login.StatusCode;
                Assert.Equal(HttpStatusCode.OK, login.StatusCode);
                var loginBody = await login.Content.ReadFromJsonAsync<LoginResponse>();
                accessIssued = !string.IsNullOrEmpty(loginBody?.Token);
                var cookie = login.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("__Host-blazorshop-refresh=", StringComparison.Ordinal));
                cookieSecure = cookie.Contains("secure", StringComparison.OrdinalIgnoreCase);
                cookieHttpOnly = cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase);
                cookieLax = cookie.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase);
                Assert.True(accessIssued && cookieSecure && cookieHttpOnly && cookieLax);
                await InspectCommerceBoundariesAsync(http, context, loginBody!.Token!, results);
            }
            else
            {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Null(user);
                if (mode == "closed-port") await CheckCommittedBankOrderDuringMailFailureAsync(app, http, context, results);
            }
            results.Add(new
            {
                mode,
                httpStatus = (int)response.StatusCode,
                userRetained = user is not null,
                acceptedMessages = sink.Messages.Count,
                confirmationStatus,
                loginStatus,
                accessIssued,
                cookieSecure,
                cookieHttpOnly,
                cookieLax
            });
        }
        await File.WriteAllTextAsync(Path.Combine(AuditPaths.Root, "smtp-results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task InspectCommerceBoundariesAsync(HttpClient http, AppDbContext db, string token, List<object> results)
    {
        var visibleCategory = new Category { Id = Guid.NewGuid(), Name = "Audit published category", Slug = "audit-visible", IsPublished = true };
        var hiddenCategory = new Category { Id = Guid.NewGuid(), Name = "Audit hidden category", Slug = "audit-hidden", IsPublished = false };
        var visible = new Product { Id = Guid.NewGuid(), Name = "Audit product", Slug = "audit-product", Description = "Synthetic isolated audit product", Price = 10, Quantity = 20, IsPublished = true, PublishedOn = DateTime.UtcNow, CategoryId = visibleCategory.Id };
        var hidden = new Product { Id = Guid.NewGuid(), Name = "Audit hidden-parent product", Slug = "audit-hidden-product", Description = visible.Description, Price = 10, Quantity = 20, IsPublished = true, PublishedOn = DateTime.UtcNow, CategoryId = hiddenCategory.Id };
        db.Categories.AddRange(visibleCategory, hiddenCategory); db.Products.AddRange(visible, hidden); await db.SaveChangesAsync();
        using var publicLookup = await http.GetAsync($"/api/product/single/{hidden.Id}");
        using var excessivePage = await http.GetAsync("/api/public/catalog/products?pageNumber=2147483647&pageSize=100");
        results.Add(new { mode = "catalog-page-overflow", httpStatus = (int)excessivePage.StatusCode });
        var methods = await db.PaymentMethods.ToListAsync();
        foreach (var kind in new[] { "Cash on Delivery", "Bank Transfer" })
        {
            var product = kind == "Cash on Delivery" ? hidden : visible;
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/cart/checkout");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            request.Content = JsonContent.Create(new { paymentMethodId = methods.Single(method => method.Name == kind).Id, carts = new[] { new { productId = product.Id, variantId = (Guid?)null, quantity = 1 } } });
            using var response = await http.SendAsync(request);
            var body = await response.Content.ReadFromJsonAsync<ServiceResponse<CheckoutResult>>();
            if (kind == "Bank Transfer") results.Add(new
            {
                mode = "empty-bank-config",
                httpStatus = (int)response.StatusCode,
                success = body?.Success,
                fallbackIbanSupplied = !string.IsNullOrEmpty(body?.Payload?.BankTransfer?.Iban),
                missingBeneficiary = string.IsNullOrEmpty(body?.Payload?.BankTransfer?.Beneficiary),
                missingBankName = string.IsNullOrEmpty(body?.Payload?.BankTransfer?.BankName)
            });
            else results.Add(new { mode = "hidden-category-checkout", httpStatus = (int)response.StatusCode, success = body?.Success, publicLookupStatus = (int)publicLookup.StatusCode });
        }
    }

    private static async Task CheckCommittedBankOrderDuringMailFailureAsync(WebApplication app, HttpClient http, AppDbContext db, List<object> results)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var password = $"Aa1!{Guid.NewGuid():N}";
        var user = new AppUser { Email = $"bank-{Guid.NewGuid():N}@example.test", UserName = $"bank-{Guid.NewGuid():N}@example.test", FullName = "Synthetic Bank Customer", EmailConfirmed = true, RequirePasswordChange = true };
        Assert.True((await users.CreateAsync(user, password)).Succeeded); Assert.True((await users.AddToRoleAsync(user, "User")).Succeeded);
        using var login = await http.PostAsJsonAsync("/api/authentication/login", new LoginUser { Email = user.Email, Password = password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
        var settings = scope.ServiceProvider.GetRequiredService<IOptions<BankTransferSettings>>().Value;
        settings.Iban = "synthetic-not-a-payment-account"; settings.Beneficiary = "Synthetic audit"; settings.BankName = "Synthetic audit";
        var product = await db.Products.AsNoTracking().FirstAsync(product => product.Slug == "audit-product");
        var method = await db.PaymentMethods.SingleAsync(method => method.Name == "Bank Transfer");
        var key = Guid.NewGuid().ToString();
        var statuses = new List<int>(); var replays = new List<bool>();
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/cart/checkout");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Add("Idempotency-Key", key);
            request.Content = JsonContent.Create(new { paymentMethodId = method.Id, carts = new[] { new { productId = product.Id, quantity = 1 } } });
            using var response = await http.SendAsync(request); statuses.Add((int)response.StatusCode);
            replays.Add(response.Headers.Contains("Idempotency-Replayed"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        var count = await db.Orders.CountAsync(order => order.UserId == user.Id);
        Assert.Equal(1, count); Assert.True(replays[1]);
        results.Add(new { mode = "bank-order-smtp-down", statuses, replays, orderCount = count, requirePasswordChangeFlagLoginStatus = (int)login.StatusCode });
    }

    private static async Task<WebApplication> CreateHostAsync(string connection, int port)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development", ContentRootPath = AppContext.BaseDirectory });
        builder.WebHost.UseTestServer(); builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connection,
            ["JWT:Key"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
            ["JWT:Issuer"] = "isolated-audit",
            ["JWT:Audience"] = "isolated-audit",
            ["ClientApp:BaseUrl"] = "https://account.example.test",
            ["Commerce:Currency"] = "EUR",
            ["Identity:RequireConfirmedAccount"] = "true",
            ["Identity:RequireConfirmedEmail"] = "true",
            ["EmailSettings:Enabled"] = "true",
            ["EmailSettings:From"] = "audit@example.test",
            ["EmailSettings:DisplayName"] = "Synthetic audit",
            ["EmailSettings:SmtpServer"] = "127.0.0.1",
            ["EmailSettings:Port"] = port.ToString(),
            ["EmailSettings:UseSsl"] = "false",
            ["Demo:Enabled"] = "false",
            ["Stripe:Enabled"] = "false",
        });
        builder.Services.AddApplication(builder.Configuration); builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddOptions<IdentityConfirmationOptions>().Bind(builder.Configuration.GetSection(IdentityConfirmationOptions.SectionName));
        builder.Services.Configure<ApiRuntimeOptions>(options => options.Security.RefreshTokenCookieSameSite = "Lax"); builder.Services.AddAuthorization();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthenticationController).Assembly);
        builder.Services.AddRateLimiter(options => options.AddPolicy("AuthApi", _ => RateLimitPartition.GetNoLimiter("isolated-mail-audit")));
        var app = builder.Build(); app.UseInfrastructure(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter(); app.MapControllers();
        await app.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync("User")) Assert.True((await roles.CreateAsync(new IdentityRole("User"))).Succeeded);
        return app;
    }

    private sealed class DatabaseScope : IAsyncDisposable
    {
        public PostgreSqlFixture Fixture { get; } = new();
        public Task InitializeAsync() => Fixture.InitializeAsync();
        public async ValueTask DisposeAsync() => await Fixture.DisposeAsync();
    }

    private sealed class SmtpSink : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource cancellation = new();
        private readonly Task loop;
        private readonly string mode;
        public int Port { get; }
        public List<string> Messages { get; } = [];
        public SmtpSink(string mode)
        {
            this.mode = mode; listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            if (mode == "closed-port") { listener.Stop(); loop = Task.CompletedTask; }
            else loop = RunAsync();
        }
        private async Task RunAsync()
        {
            try
            {
                while (!cancellation.IsCancellationRequested)
                {
                    using var client = await listener.AcceptTcpClientAsync(cancellation.Token);
                    using var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
                    await writer.WriteLineAsync("220 localhost audit SMTP");
                    while (await reader.ReadLineAsync(cancellation.Token) is { } line)
                    {
                        if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase)) await writer.WriteLineAsync("250-localhost\r\n250 SIZE 1000000");
                        else if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
                        {
                            await writer.WriteLineAsync("354 End with dot");
                            var message = new StringBuilder();
                            while (await reader.ReadLineAsync(cancellation.Token) is { } data && data != ".") message.AppendLine(data);
                            Messages.Add(message.ToString());
                            if (mode == "accepted-data-lost-ack") break;
                            await writer.WriteLineAsync("250 accepted");
                        }
                        else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase)) { await writer.WriteLineAsync("221 bye"); break; }
                        else await writer.WriteLineAsync("250 OK");
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException) when (cancellation.IsCancellationRequested) { }
        }
        public async ValueTask DisposeAsync() { cancellation.Cancel(); listener.Stop(); await loop; cancellation.Dispose(); }
    }
}

internal static class AuditPaths
{
    public static string Root => Environment.GetEnvironmentVariable("BLAZORSHOP_RC_AUDIT_DIR") ?? throw new InvalidOperationException("Audit output directory required.");
}
