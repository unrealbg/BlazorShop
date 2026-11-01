namespace BlazorShop.Tests.Infrastructure.PostgreSql;

using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using BlazorShop.Application.DTOs.UserIdentity;
using BlazorShop.Application.Options;
using BlazorShop.Domain.Entities;
using BlazorShop.Domain.Entities.Payment;
using BlazorShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static RefreshEligibilityHttpTests;

[Collection(PostgreSqlCollection.Name)]
public sealed class RegistrationConfirmationMigrationTests(PostgreSqlFixture database) : IAsyncLifetime
{
    private const string Previous = "20260909075202_ArchiveLegacyCheckoutOrderItems";
    private const string Current = "20261101131138_AddRegistrationConfirmationOutbox";
    public Task InitializeAsync() => database.ResetDatabaseAsync();
    public async Task DisposeAsync()
    {
        await using var db = database.CreateContext();
        await db.Database.MigrateAsync();
    }

    [Fact]
    public async Task AdditiveUpgradeAndEmptyRollback_PreserveIdentityAndCommerce_NonemptyRollbackIsRefused()
    {
        await using var db = database.CreateContext();
        await db.Database.MigrateAsync(Previous);
        await using var api = await IdentityHttpHost.CreateAsync(database.ConnectionString, configureServices: services =>
            services.Configure<RegistrationConfirmationOptions>(options => options.WorkerEnabled = false));
        var user = await api.CreateUserAsync("User", confirmed: false);
        var category = new Category { Name = "Synthetic migration category", Slug = $"migration-{Guid.NewGuid():N}", IsPublished = true };
        var product = new Product { Name = "Synthetic migration product", Slug = $"migration-{Guid.NewGuid():N}", Description = "Preservation fixture",
            Quantity = 4, Price = 10, Category = category, IsPublished = true, PublishedOn = DateTime.UtcNow };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var buyer = await api.CreateUserAsync("User");
        var customer = await api.LoginAsync(buyer);
        using (var checkout = new HttpRequestMessage(HttpMethod.Post, "/api/cart/checkout"))
        {
            checkout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", customer.AccessToken);
            checkout.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            checkout.Content = JsonContent.Create(new { paymentMethodId = PaymentMethodIds.CashOnDelivery,
                carts = new[] { new { productId = product.Id, quantity = 1 } } });
            using var placed = await api.SendRawAsync(checkout);
            Assert.Equal(HttpStatusCode.OK, placed.StatusCode);
        }
        Assert.Equal(1, await db.Orders.CountAsync());
        Assert.Equal(1, await db.InventoryReservations.CountAsync());
        Assert.Equal(1, await db.CheckoutIdempotencyRecords.CountAsync());
        Assert.True(await db.RefreshTokens.AnyAsync(token => token.UserId == buyer.Id));
        var before = await ExistingDataFingerprintAsync(db);
        await db.Database.MigrateAsync();
        Assert.Contains(Current, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(before, await ExistingDataFingerprintAsync(db));
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/login")
        {
            Content = JsonContent.Create(new LoginUser { Email = user.Email, Password = IdentityHttpHost.Password }),
        };
        using var response = await api.SendRawAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(1, await db.RegistrationConfirmationDeliveries.CountAsync());
        Assert.Equal(before, await ExistingDataFingerprintAsync(db));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.MigrateAsync(Previous));
        Assert.Contains(Current, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(1, await db.RegistrationConfirmationDeliveries.CountAsync());
        Assert.Equal(before, await ExistingDataFingerprintAsync(db));
        // Only the disposable fixture's intent is removed to exercise the empty Down path.
        await db.RegistrationConfirmationDeliveries.ExecuteDeleteAsync();
        await db.Database.MigrateAsync(Previous);
        Assert.DoesNotContain(Current, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(before, await ExistingDataFingerprintAsync(db));
        await db.Database.MigrateAsync();
        Assert.Equal(before, await ExistingDataFingerprintAsync(db));
    }

    [Fact]
    public async Task MissingOutboxSchema_FailsRegistrationWithoutLeavingIdentityData()
    {
        await using var db = database.CreateContext();
        await db.Database.MigrateAsync(Previous);
        await using var api = await IdentityHttpHost.CreateAsync(database.ConnectionString, configureServices: services =>
            services.Configure<RegistrationConfirmationOptions>(options => options.WorkerEnabled = false));
        var email = $"schema-failure-{Guid.NewGuid():N}@example.test";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/authentication/create")
        {
            Content = JsonContent.Create(new CreateUser { Email = email, FullName = "Synthetic migration customer",
                Password = IdentityHttpHost.Password, ConfirmPassword = IdentityHttpHost.Password }),
        };
        using var response = await api.SendRawAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await db.Users.CountAsync(user => user.Email == email));
        Assert.DoesNotContain(Current, await db.Database.GetAppliedMigrationsAsync());
        await db.Database.MigrateAsync();
        Assert.Equal(0, await db.RegistrationConfirmationDeliveries.CountAsync());
    }

    private static async Task<string> ExistingDataFingerprintAsync(AppDbContext db)
    {
        var data = new StringBuilder();
        await db.Database.OpenConnectionAsync();
        foreach (var table in new[] { "AspNetUsers", "AspNetUserRoles", "AspNetRoles", "Products", "Categories", "Orders", "OrderLines",
            "InventoryReservations", "CheckoutIdempotencyRecords", "PaymentTransactions", "RefreshTokens", "AdminSettings", "SeoSettings" })
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT COALESCE(jsonb_agg(to_jsonb(t) ORDER BY to_jsonb(t)::text)::text, '[]') FROM \"{table}\" t";
            data.Append(await command.ExecuteScalarAsync());
        }
        // Compare fingerprints, never log Identity password hashes/security stamps or rows.
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToString())));
    }
}
