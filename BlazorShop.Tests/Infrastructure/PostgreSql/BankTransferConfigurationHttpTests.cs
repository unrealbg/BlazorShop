namespace BlazorShop.Tests.Infrastructure.PostgreSql;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using BlazorShop.Application.DTOs;
using BlazorShop.Application.DTOs.Payment;
using BlazorShop.Application.Options;
using BlazorShop.Application.Services.Contracts.Payment;
using BlazorShop.Domain.Contracts;
using BlazorShop.Domain.Contracts.Payment;
using BlazorShop.Domain.Entities;
using BlazorShop.Domain.Entities.Payment;
using BlazorShop.Testing;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

using static RefreshEligibilityHttpTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class BankTransferConfigurationHttpTests(PostgreSqlFixture database) : IAsyncLifetime
{
    public Task InitializeAsync() => database.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    [InlineData("missing-iban")]
    [InlineData("blank-iban")]
    [InlineData("beneficiary")]
    [InlineData("bank-name")]
    [InlineData("placeholder-iban")]
    [InlineData("format")]
    [InlineData("checksum")]
    [InlineData("placeholder-beneficiary")]
    [InlineData("placeholder-bank")]
    public async Task InvalidSettings_HideBankAndRejectFreshCheckoutWithoutCommerceSideEffects(string invalidField)
    {
        var settings = ValidSettings();
        switch (invalidField)
        {
            case "missing-iban": settings.Iban = null!; break;
            case "blank-iban": settings.Iban = " \t "; break;
            case "beneficiary": settings.Beneficiary = " "; break;
            case "bank-name": settings.BankName = null!; break;
            case "placeholder-iban": settings.Iban = "YOUR_IBAN"; break;
            case "format": settings.Iban = "not-a-bank-account"; break;
            case "checksum":
                // Keep the structure/check-digit range valid so only MOD-97 can reject it.
                settings.Iban = (settings.Iban.StartsWith("BG02", StringComparison.Ordinal) ? "BG03" : "BG02") + settings.Iban[4..];
                break;
            case "placeholder-beneficiary": settings.Beneficiary = "<YOUR_BENEFICIARY>"; break;
            case "placeholder-bank": settings.BankName = "CHANGE_ME"; break;
        }

        var email = new Mock<IEmailService>(MockBehavior.Strict);
        await using var api = await CreateHostAsync(settings, email);
        var session = await api.LoginAsync(await api.CreateUserAsync("User"));
        var product = await SeedProductAsync();
        var methods = await GetMethodsAsync(api);
        Assert.Contains(methods, method => method.Id == PaymentMethodIds.CashOnDelivery);
        Assert.Contains(methods, method => method.Id == PaymentMethodIds.CreditCard);

        var key = Guid.NewGuid();
        using var rejected = await CheckoutAsync(api, session, product.Id, key);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.DoesNotContain(methods, method => method.Id == PaymentMethodIds.BankTransfer);
        var failure = await rejected.Content.ReadFromJsonAsync<ServiceResponse<CheckoutResult>>();
        Assert.False(failure!.Success);
        Assert.Null(failure.Payload);
        using var replay = await CheckoutAsync(api, session, product.Id, key);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.True(replay.Headers.Contains("Idempotency-Replayed"));

        await using var db = database.CreateContext();
        Assert.Empty(await db.Orders.ToListAsync());
        Assert.Empty(await db.OrderLines.ToListAsync());
        Assert.Empty(await db.InventoryReservations.ToListAsync());
        Assert.Empty(await db.PaymentTransactions.ToListAsync());
        Assert.Equal(5, (await db.Products.FindAsync(product.Id))!.Quantity);
        var claim = await db.CheckoutIdempotencyRecords.SingleAsync();
        Assert.Equal(CheckoutIdempotencyState.Failed, claim.State);
        Assert.NotNull(claim.OutcomeJson);
        var outcome = JsonSerializer.Deserialize<PersistedCheckoutOutcome>(claim.OutcomeJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.False(outcome!.Response.Success);
        Assert.Null(outcome.Response.Payload);
        email.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidSettings_CreateOnePendingOrderAndReplayAfterConfigurationRemoval(bool normalizeInput)
    {
        var settings = ValidSettings();
        var expectedIban = settings.Iban;
        if (normalizeInput)
        {
            settings.Iban = " " + settings.Iban[..4].ToLowerInvariant() + " " + settings.Iban[4..].ToLowerInvariant() + " ";
            settings.Beneficiary = " " + settings.Beneficiary + " ";
            settings.BankName = " " + settings.BankName + " ";
            settings.AdditionalInfo = null;
        }
        var email = RecordingEmail();
        var user = default(TestUser)!;
        var product = await SeedProductAsync();
        var key = Guid.NewGuid();
        string original;
        await using (var api = await CreateHostAsync(settings, email))
        {
            user = await api.CreateUserAsync("User");
            var session = await api.LoginAsync(user);
            Assert.Contains(await GetMethodsAsync(api), method => method.Id == PaymentMethodIds.BankTransfer);
            using var first = await CheckoutAsync(api, session, product.Id, key);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            original = await first.Content.ReadAsStringAsync();
            var result = await first.Content.ReadFromJsonAsync<ServiceResponse<CheckoutResult>>();
            Assert.True(result!.Success);
            Assert.Equal(CheckoutStatus.PendingPayment, result.Payload!.Status);
            var info = result.Payload.BankTransfer!;
            Assert.Equal(expectedIban, info.Iban);
            Assert.Equal(settings.Beneficiary.Trim(), info.Beneficiary);
            Assert.Equal(settings.BankName.Trim(), info.BankName);
            Assert.Equal(settings.AdditionalInfo, info.AdditionalInfo);
            Assert.Equal(25m, info.Amount);
            Assert.Equal("EUR", info.Currency);
            Assert.Equal(result.Payload.OrderReference, info.Reference);
        }

        await using var unavailable = await CreateHostAsync(new BankTransferSettings(), email);
        var laterSession = await unavailable.LoginAsync(user);
        Assert.DoesNotContain(await GetMethodsAsync(unavailable), method => method.Id == PaymentMethodIds.BankTransfer);
        using var replay = await CheckoutAsync(unavailable, laterSession, product.Id, key);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal(original, await replay.Content.ReadAsStringAsync());
        using var conflict = await CheckoutAsync(unavailable, laterSession, product.Id, key, quantity: 2);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var fresh = await CheckoutAsync(unavailable, laterSession, product.Id, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, fresh.StatusCode);
        await AssertOneBankOrderAsync(product.Id);
        email.Verify(service => service.SendEmailAsync(It.IsAny<string>(), "Bank Transfer Instructions", It.IsAny<string>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CommittedOrderAfterConfigurationRemoval_UsesSnapshotOrRequiresRecovery(bool hasSnapshot)
    {
        var email = RecordingEmail();
        var product = await SeedProductAsync();
        var key = Guid.NewGuid();
        TestUser user;
        string original;
        await using (var api = await CreateHostAsync(ValidSettings(), email))
        {
            user = await api.CreateUserAsync("User");
            using var first = await CheckoutAsync(api, await api.LoginAsync(user), product.Id, key);
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            original = await first.Content.ReadAsStringAsync();
        }

        // Recreate the durable crash boundary: order/reservation and pending outcome committed,
        // completion/email not acknowledged. Do not fabricate or alter the stored instructions.
        await using (var db = database.CreateContext())
        {
            var claim = await db.CheckoutIdempotencyRecords.SingleAsync();
            claim.State = CheckoutIdempotencyState.LocalCommitted;
            claim.CompletedOn = null;
            claim.ExpiresOn = null;
            claim.LeaseExpiresOn = DateTime.UtcNow.AddMinutes(-1);
            if (!hasSnapshot) claim.OutcomeJson = null;
            await db.SaveChangesAsync();
        }

        await using var recovery = await CreateHostAsync(new BankTransferSettings(), email);
        var session = await recovery.LoginAsync(user);
        using var recovered = await CheckoutAsync(recovery, session, product.Id, key);
        Assert.Equal(hasSnapshot ? HttpStatusCode.OK : HttpStatusCode.Conflict, recovered.StatusCode);
        if (hasSnapshot) Assert.Equal(original, await recovered.Content.ReadAsStringAsync());
        else
        {
            var result = await recovered.Content.ReadFromJsonAsync<ServiceResponse<CheckoutResult>>();
            Assert.False(result!.Success);
            Assert.Null(result.Payload);
            await using var db = database.CreateContext();
            var claim = await db.CheckoutIdempotencyRecords.SingleAsync();
            Assert.Equal(CheckoutIdempotencyState.LocalCommitted, claim.State);
            Assert.Null(claim.OutcomeJson);
            Assert.Null(claim.LeaseOwnerId);
        }
        await AssertOneBankOrderAsync(product.Id);
        email.Verify(service => service.SendEmailAsync(It.IsAny<string>(), "Bank Transfer Instructions", It.IsAny<string>()),
            hasSnapshot ? Times.Exactly(2) : Times.Once());
    }

    [Fact]
    public async Task StaleDiscoveryAdvertisingBank_CannotBypassIndependentExecutionGuard()
    {
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        await using var api = await IdentityHttpHost.CreateAsync(database.ConnectionString, configureServices: services =>
        {
            services.AddSingleton(email.Object);
            var methods = new Mock<IPaymentMethodService>();
            methods.Setup(service => service.GetPaymentMethodsAsync())
                .ReturnsAsync([new GetPaymentMethod { Id = PaymentMethodIds.BankTransfer, Name = "Stale advertised method" }]);
            services.AddSingleton(methods.Object);
        });
        var product = await SeedProductAsync();
        var session = await api.LoginAsync(await api.CreateUserAsync("User"));
        Assert.Contains(await GetMethodsAsync(api), method => method.Id == PaymentMethodIds.BankTransfer);
        using var rejected = await CheckoutAsync(api, session, product.Id, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var failure = await rejected.Content.ReadFromJsonAsync<ServiceResponse<CheckoutResult>>();
        Assert.False(failure!.Success);
        Assert.Null(failure.Payload);
        await using var db = database.CreateContext();
        Assert.Empty(await db.Orders.ToListAsync());
        Assert.Empty(await db.OrderLines.ToListAsync());
        Assert.Empty(await db.InventoryReservations.ToListAsync());
        Assert.Empty(await db.PaymentTransactions.ToListAsync());
        Assert.Equal(5, (await db.Products.FindAsync(product.Id))!.Quantity);
        var claim = await db.CheckoutIdempotencyRecords.SingleAsync();
        Assert.Equal(CheckoutIdempotencyState.Failed, claim.State);
        Assert.DoesNotContain("iban", claim.OutcomeJson!, StringComparison.OrdinalIgnoreCase);
        email.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConfigurationCorrection_RequiresFreshKeyAndRetainsFailedReplay()
    {
        var email = RecordingEmail();
        var product = await SeedProductAsync();
        var key = Guid.NewGuid();
        TestUser user;
        string failure;
        await using (var invalid = await CreateHostAsync(new BankTransferSettings(), email))
        {
            user = await invalid.CreateUserAsync("User");
            using var rejected = await CheckoutAsync(invalid, await invalid.LoginAsync(user), product.Id, key);
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
            failure = await rejected.Content.ReadAsStringAsync();
        }
        await using var corrected = await CreateHostAsync(ValidSettings(), email);
        var session = await corrected.LoginAsync(user);
        using var replay = await CheckoutAsync(corrected, session, product.Id, key);
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.True(replay.Headers.Contains("Idempotency-Replayed"));
        Assert.Equal(failure, await replay.Content.ReadAsStringAsync());
        using var fresh = await CheckoutAsync(corrected, session, product.Id, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        await AssertOneBankOrderAsync(product.Id);
        email.Verify(service => service.SendEmailAsync(It.IsAny<string>(), "Bank Transfer Instructions", It.IsAny<string>()), Times.Once);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("paypal")]
    public async Task UnsupportedMethod_RejectsWithoutCommerceSideEffects(string method)
    {
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        await using var api = await CreateHostAsync(ValidSettings(), email);
        var session = await api.LoginAsync(await api.CreateUserAsync("User"));
        var product = await SeedProductAsync();
        await using var db = database.CreateContext();
        if (method == "paypal")
        {
            db.PaymentMethods.Add(new PaymentMethod { Id = Guid.NewGuid(), Name = "PayPal" });
            await db.SaveChangesAsync();
        }
        var methodId = method == "unknown" ? Guid.NewGuid() : (await db.PaymentMethods.SingleAsync(method => method.Name == "PayPal")).Id;
        using var response = await CheckoutAsync(api, session, product.Id, Guid.NewGuid(),
            methodId: methodId);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await db.Orders.ToListAsync());
        Assert.Empty(await db.InventoryReservations.ToListAsync());
        Assert.Equal(5, (await db.Products.FindAsync(product.Id))!.Quantity);
        email.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingBankSettings_DoNotBlockCodOrEnabledStripe(bool stripe)
    {
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        var provider = new Mock<IPaymentService>(MockBehavior.Strict);
        provider.Setup(service => service.Pay(It.IsAny<StripeCheckoutInitialization>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaymentInitializationResult(true, "https://checkout.stripe.test/synthetic", ProviderSessionId: "cs_synthetic_bank_guard"));
        await using var api = await CreateHostAsync(new BankTransferSettings(), email, provider.Object);
        var methods = await GetMethodsAsync(api);
        Assert.Contains(methods, method => method.Id == PaymentMethodIds.CreditCard);
        Assert.Contains(methods, method => method.Id == PaymentMethodIds.CashOnDelivery);
        var product = await SeedProductAsync();
        using var response = await CheckoutAsync(api, await api.LoginAsync(await api.CreateUserAsync("User")),
            product.Id, Guid.NewGuid(), methodId: stripe ? PaymentMethodIds.CreditCard : PaymentMethodIds.CashOnDelivery);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ServiceResponse<CheckoutResult>>();
        Assert.True(result!.Success);
        Assert.Equal(stripe ? CheckoutPaymentKind.Stripe : CheckoutPaymentKind.CashOnDelivery, result.Payload!.PaymentKind);
        Assert.Null(result.Payload.BankTransfer);
        await using var db = database.CreateContext();
        Assert.Single(await db.Orders.ToListAsync());
        Assert.Equal(stripe ? InventoryReservationStatus.Reserved : InventoryReservationStatus.Consumed, (await db.InventoryReservations.SingleAsync()).Status);
        provider.Verify(service => service.Pay(It.IsAny<StripeCheckoutInitialization>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            stripe ? Times.Once() : Times.Never());
        email.VerifyNoOtherCalls();
    }

    private static BankTransferSettings ValidSettings() => new()
    {
        Iban = SyntheticBankTransfer.CreateIban(),
        Beneficiary = "Synthetic regression merchant",
        BankName = "Synthetic regression institution",
        AdditionalInfo = "Synthetic fixture only; never transfer funds.",
    };

    private static Mock<IEmailService> RecordingEmail()
    {
        var email = new Mock<IEmailService>(MockBehavior.Strict);
        email.Setup(service => service.SendEmailAsync(It.IsAny<string>(), "Bank Transfer Instructions", It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        return email;
    }

    private Task<IdentityHttpHost> CreateHostAsync(BankTransferSettings settings, Mock<IEmailService> email, IPaymentService? provider = null) =>
        IdentityHttpHost.CreateAsync(database.ConnectionString, configureServices: services =>
        {
            services.Configure<BankTransferSettings>(options =>
            {
                options.Iban = settings.Iban;
                options.Beneficiary = settings.Beneficiary;
                options.BankName = settings.BankName;
                options.AdditionalInfo = settings.AdditionalInfo;
            });
            services.Configure<StripeOptions>(options => options.Enabled = true);
            services.AddSingleton(email.Object);
            if (provider is not null) services.AddSingleton(provider);
        });

    private static async Task<List<GetPaymentMethod>> GetMethodsAsync(IdentityHttpHost api)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/payment/methods");
        using var response = await api.SendRawAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<List<GetPaymentMethod>>())!;
    }

    private static async Task<HttpResponseMessage> CheckoutAsync(IdentityHttpHost api, Session session, Guid productId,
        Guid key, int quantity = 1, Guid? methodId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/cart/checkout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        request.Headers.Add("Idempotency-Key", key.ToString());
        request.Content = JsonContent.Create(new Checkout
        {
            PaymentMethodId = methodId ?? PaymentMethodIds.BankTransfer,
            Carts = [new CartLineRequest(productId, null, quantity)],
        });
        return await api.SendRawAsync(request);
    }

    private async Task<Product> SeedProductAsync()
    {
        await using var db = database.CreateContext();
        var category = new Category { Name = "Synthetic regression category", Slug = $"bank-{Guid.NewGuid():N}", IsPublished = true };
        var product = new Product
        {
            Name = "Synthetic regression product", Slug = $"bank-{Guid.NewGuid():N}", Description = "Isolated bank configuration regression",
            Price = 25, Quantity = 5, IsPublished = true, PublishedOn = DateTime.UtcNow, Category = category,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private async Task AssertOneBankOrderAsync(Guid productId)
    {
        await using var db = database.CreateContext();
        var order = await db.Orders.SingleAsync();
        Assert.Equal(OrderPaymentMethod.BankTransfer, order.PaymentMethod);
        Assert.Equal(OrderPaymentStatus.Pending, order.PaymentStatus);
        Assert.Equal(25m, order.TotalAmount);
        Assert.Equal("EUR", order.Currency);
        Assert.Single(await db.OrderLines.ToListAsync());
        Assert.Equal(InventoryReservationStatus.Reserved, (await db.InventoryReservations.SingleAsync()).Status);
        Assert.Empty(await db.PaymentTransactions.ToListAsync());
        Assert.Equal(4, (await db.Products.FindAsync(productId))!.Quantity);
    }
}
