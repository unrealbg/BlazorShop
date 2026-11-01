namespace BlazorShop.Tests.Infrastructure.PostgreSql;

using System.Net;
using System.Net.Http.Json;
using BlazorShop.Application.DTOs;
using BlazorShop.Application.DTOs.UserIdentity;
using BlazorShop.Application.Options;
using BlazorShop.Domain.Entities.Identity;
using BlazorShop.Infrastructure.Services;
using BlazorShop.Tests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static RefreshEligibilityHttpTests;

[Collection(PostgreSqlCollection.Name)]
public sealed partial class RegistrationConfirmationHttpTests(PostgreSqlFixture database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("healthy")]
    [InlineData("closed")]
    [InlineData("lost-ack")]
    public async Task StrictRegistration_CommitsIntentWithoutSmtpAndKeepsAccountDuringDeliveryFailure(string mode)
    {
        await using var smtp = new LoopbackSmtpSink(mode switch
        {
            "closed" => SmtpTestMode.ClosedPort,
            "lost-ack" => SmtpTestMode.LostAcknowledgement,
            _ => SmtpTestMode.Healthy,
        });
        await using var api = await CreateHostAsync(smtp);
        var registration = NewRegistration();
        using var response = await RegisterAsync(api, registration);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<ServiceResponse>())!.Success);
        Assert.True(smtp.Messages.Count == 0, "Registration dispatched SMTP in the HTTP request.");
        var user = await FindUserAsync(registration.Email);
        Assert.False(user.EmailConfirmed);
        Assert.Equal(ConfirmationDeliveryState.Pending, (await IntentAsync()).State);
        using var blocked = await LoginAsync(api, registration);
        await AssertNoCredentialsAsync(blocked);
        Assert.Equal(0, (await IntentAsync()).AttemptCount);

        Assert.Equal(1, await DispatchAsync(api));
        Assert.Equal(mode == "healthy" ? ConfirmationDeliveryState.Delivered : ConfirmationDeliveryState.Pending, (await IntentAsync()).State);
        Assert.Equal(user.Id, (await FindUserAsync(registration.Email)).Id);
        Assert.False((await FindUserAsync(registration.Email)).EmailConfirmed);
        if (mode != "closed")
        {
            Assert.True(smtp.Messages.Count == 1, "Unexpected confirmation delivery count.");
            using var confirmation = await ConfirmAsync(api, smtp.ConfirmationLink());
            Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
            using var login = await LoginAsync(api, registration);
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            Assert.True((await login.Content.ReadFromJsonAsync<LoginResponse>())!.Success);
        }
    }

    private Task<IdentityHttpHost> CreateHostAsync(LoopbackSmtpSink smtp, bool enabled = true,
        bool requireAccount = true, bool requireEmail = true, Action<IServiceCollection>? configure = null, bool demoEnabled = false) =>
        IdentityHttpHost.CreateAsync(database.ConnectionString, requireAccount: requireAccount, requireEmail: requireEmail,
            demoEnabled: demoEnabled,
            configureServices: services =>
            {
                services.Configure<EmailSettings>(options =>
                {
                    options.Enabled = enabled;
                    options.From = "registration@example.test";
                    options.DisplayName = "Synthetic registration store";
                    options.SmtpServer = "127.0.0.1";
                    options.Port = smtp.Port;
                    options.UseSsl = false;
                    options.Username = options.Password = string.Empty;
                });
                services.Configure<RegistrationConfirmationOptions>(options => options.WorkerEnabled = false);
                configure?.Invoke(services);
            });

    private static CreateUser NewRegistration() => new()
    {
        Email = $"registration-{Guid.NewGuid():N}@example.test", FullName = "Synthetic registration customer",
        Password = IdentityHttpHost.Password, ConfirmPassword = IdentityHttpHost.Password,
    };

    private static Task<HttpResponseMessage> RegisterAsync(IdentityHttpHost api, CreateUser registration) =>
        SendAsync(api, HttpMethod.Post, "/api/authentication/create", registration);
    private static Task<HttpResponseMessage> LoginAsync(IdentityHttpHost api, CreateUser registration) =>
        SendAsync(api, HttpMethod.Post, "/api/authentication/login", new LoginUser { Email = registration.Email, Password = registration.Password });
    private static async Task<HttpResponseMessage> SendAsync(IdentityHttpHost api, HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await api.SendRawAsync(request);
    }
    private static Task<HttpResponseMessage> ConfirmAsync(IdentityHttpHost api, Uri link) =>
        SendAsync(api, HttpMethod.Get, "/api/authentication/confirm-email" + link.Query);
    private async Task<AppUser> FindUserAsync(string email)
    {
        await using var db = database.CreateContext();
        return await db.Users.AsNoTracking().SingleAsync(user => user.Email == email);
    }
    private async Task<RegistrationConfirmationDelivery> IntentAsync()
    {
        await using var db = database.CreateContext();
        return await db.RegistrationConfirmationDeliveries.AsNoTracking().SingleAsync();
    }
    private static async Task<int> DispatchAsync(IdentityHttpHost api)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<RegistrationConfirmationDispatcher>().DispatchBatchAsync();
    }
    private static async Task<LoginResponse> AssertNoCredentialsAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.False(body.Success);
        Assert.True(string.IsNullOrEmpty(body.Token) && string.IsNullOrEmpty(body.RefreshToken), "Unconfirmed login issued credentials.");
        Assert.False(response.Headers.Contains("Set-Cookie"));
        return body;
    }
}
