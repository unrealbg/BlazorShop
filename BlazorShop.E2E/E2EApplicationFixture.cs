namespace BlazorShop.E2E;

using System.Globalization;
using System.Security.Cryptography;

using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

using BlazorShop.Domain.Entities;
using BlazorShop.Domain.Entities.Identity;
using BlazorShop.Domain.Entities.Payment;
using BlazorShop.Infrastructure.Data;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public sealed class E2EApplicationFixture : IAsyncLifetime
{
    private const string PostgresImageTag = "16.13-alpine3.23";
    private const string UserRoleId = "b7af6842-02fa-4af4-8f61-ae04a49644a2";
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(3);
    private DistributedApplication? application;
    private string? connectionString;

    public Uri StorefrontBaseUrl { get; private set; } = null!;

    public Uri WebBaseUrl { get; private set; } = null!;

    public Uri ApiBaseUrl { get; private set; } = null!;

    public string ArtifactDirectory { get; } = ResolveArtifactDirectory();

    public async Task InitializeAsync()
    {
        var args = new[]
        {
            "--environment=Development",
            "--UseVolumes=false",
            $"--PostgresImageTag={PostgresImageTag}",
            "--ApiLaunchProfile=e2e",
            "--DcpPublisher:RandomizePorts=false",
        };

        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BlazorShop_AppHost>(args);
        var apiResource = builder.Resources
            .OfType<ProjectResource>()
            .Single(resource => resource.Name == "apiservice");
        var storefrontResource = builder.Resources
            .OfType<ProjectResource>()
            .Single(resource => resource.Name == "storefront");
        var observationKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        builder.CreateResourceBuilder(apiResource)
            .WithEnvironment("Jwt__Key", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)))
            .WithEnvironment("Runtime__Security__EnableHttpsRedirection", "false")
            .WithEnvironment("Demo__Enabled", "true")
            .WithEnvironment("EmailSettings__Enabled", "false")
            .WithEnvironment("SessionObservation__Enabled", "true")
            .WithEnvironment("SessionObservation__ServiceKey", observationKey)
            .WithEnvironment("SessionObservation__AllowInsecureDevelopmentTransport", "true")
            .WithEnvironment("EmailSettings__From", "browser@example.test")
            .WithEnvironment("EmailSettings__SmtpServer", "smtp.example.test")
            .WithEnvironment("EmailSettings__DisplayName", "Synthetic browser store")
            .WithEnvironment("BankTransfer__Iban", "BG00SYNTHETIC00000000")
            .WithEnvironment("BankTransfer__Beneficiary", "Synthetic browser merchant")
            .WithEnvironment("BankTransfer__BankName", "Synthetic browser bank")
            .WithEnvironment("BankTransfer__AdditionalInfo", "Synthetic transfer instructions; no real payment")
            .WithEndpoint("https", endpoint => endpoint.Port = 7094);
        builder.CreateResourceBuilder(storefrontResource)
            .WithEnvironment("SessionObservation__Enabled", "true")
            .WithEnvironment("SessionObservation__ServiceKey", observationKey)
            .WithEnvironment("SessionObservation__AllowInsecureDevelopmentTransport", "true")
            .WithEnvironment("Api__BaseUrl", "http://apiservice/api/");

        application = await builder.BuildAsync();
        await application.StartAsync();

        await application.ResourceNotifications
            .WaitForResourceHealthyAsync("apiservice")
            .WaitAsync(StartupTimeout);

        StorefrontBaseUrl = application.GetEndpoint("storefront", "https");
        WebBaseUrl = application.GetEndpoint("adminclient", "https");
        ApiBaseUrl = application.GetEndpoint("apiservice", "https");
        connectionString = await application.GetConnectionStringAsync("DefaultConnection");

        await WaitForHttpReadyAsync(StorefrontBaseUrl);
        await WaitForHttpReadyAsync(WebBaseUrl);

        await using var dbContext = CreateDbContext();
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            throw new InvalidOperationException(
                $"The E2E database did not apply all migrations: {string.Join(", ", pendingMigrations)}");
        }
    }

    public async Task<E2ETestData> SeedScenarioAsync(bool includeListingSamples = false)
    {
        var suffix = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..10];
        var categoryId = Guid.NewGuid();
        var simpleProductId = Guid.NewGuid();
        var variantProductId = Guid.NewGuid();
        var variantId = Guid.NewGuid();
        var userId = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        var categoryName = $"E2E Footwear {suffix}";
        var categorySlug = $"e2e-footwear-{suffix}";
        var simpleProductName = $"E2E Canvas Bag {suffix}";
        var simpleProductSlug = $"e2e-canvas-bag-{suffix}";
        var simpleProductDescription = $"Deterministic browser-test bag {suffix}.";
        var variantProductName = $"E2E Trail Shoe {suffix}";
        var variantProductSlug = $"e2e-trail-shoe-{suffix}";
        var variantSku = $"E2E-US10-{suffix}";
        var email = $"e2e.user.{suffix}@blazorshop.local";
        var password = $"E2e!{Guid.NewGuid():N}aA1";

        var category = new Category
        {
            Id = categoryId,
            Name = categoryName,
            Slug = categorySlug,
            MetaDescription = $"Synthetic category {suffix} for isolated browser testing.",
            IsPublished = true,
        };
        var simpleProduct = new Product
        {
            Id = simpleProductId,
            Name = simpleProductName,
            Slug = simpleProductSlug,
            Description = simpleProductDescription,
            Image = includeListingSamples ? "/images/products/metro-runner.webp" : null,
            Price = 12.50m,
            Quantity = 20,
            CreatedOn = DateTime.UtcNow,
            PublishedOn = DateTime.UtcNow.AddMinutes(-1),
            IsPublished = true,
            CategoryId = categoryId,
        };
        var variantProduct = new Product
        {
            Id = variantProductId,
            Name = variantProductName,
            Slug = variantProductSlug,
            Description = $"Deterministic browser-test shoe {suffix}.",
            Price = 25.00m,
            Quantity = 0,
            CreatedOn = DateTime.UtcNow,
            PublishedOn = DateTime.UtcNow.AddMinutes(-1),
            IsPublished = true,
            CategoryId = categoryId,
        };
        var variant = new ProductVariant
        {
            Id = variantId,
            ProductId = variantProductId,
            Sku = variantSku,
            SizeScale = SizeScale.ShoesUS,
            SizeValue = "10",
            Price = 31.25m,
            Stock = 20,
            Color = "Blue",
            IsDefault = true,
        };
        var user = new AppUser
        {
            Id = userId,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            FullName = $"E2E Customer {suffix}",
            SecurityStamp = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
            ConcurrencyStamp = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture),
            LockoutEnabled = true,
            CreatedOn = DateTime.UtcNow,
        };
        user.PasswordHash = new PasswordHasher<AppUser>().HashPassword(user, password);

        await using var dbContext = CreateDbContext();
        dbContext.Categories.Add(category);
        dbContext.Products.AddRange(simpleProduct, variantProduct);
        if (includeListingSamples)
        {
            dbContext.Products.Add(new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Unpublished {simpleProductName}",
                Slug = $"unpublished-{simpleProductSlug}",
                Description = "Unpublished listing regression sample.",
                Price = 9.00m,
                Quantity = 5,
                CreatedOn = DateTime.UtcNow,
                IsPublished = false,
                CategoryId = categoryId,
            });
            dbContext.Products.Add(new Product
            {
                Id = Guid.NewGuid(),
                Name = $"Older {simpleProductName}",
                Slug = $"older-{simpleProductSlug}",
                Description = "Product outside the New Releases window.",
                Price = 9.00m,
                Quantity = 5,
                CreatedOn = DateTime.UtcNow.AddDays(-8),
                PublishedOn = DateTime.UtcNow.AddDays(-8),
                IsPublished = true,
                CategoryId = categoryId,
            });
        }
        dbContext.ProductVariants.Add(variant);
        dbContext.Users.Add(user);
        dbContext.UserRoles.Add(new IdentityUserRole<string>
        {
            UserId = userId,
            RoleId = UserRoleId,
        });
        await dbContext.SaveChangesAsync();

        return new E2ETestData(
            categoryId,
            categoryName,
            categorySlug,
            simpleProductId,
            simpleProductName,
            simpleProductSlug,
            simpleProductDescription,
            simpleProduct.Price,
            variantProductId,
            variantProductName,
            variantProductSlug,
            variantId,
            variant.SizeValue,
            variantSku,
            variant.Price.Value,
            userId,
            email,
            password);
    }

    public async Task<(Guid Id, string Sku, string Size, decimal Price)> SeedCartVariantAsync(E2ETestData data)
    {
        await using var dbContext = CreateDbContext();
        var product = await dbContext.Products.SingleAsync(candidate => candidate.Id == data.SimpleProductId);
        product.Image = "/images/products/meridian-daypack.webp";
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = data.VariantProductId,
            Sku = $"{data.VariantSku}-SECOND",
            SizeScale = SizeScale.ShoesUS,
            SizeValue = "11",
            Color = "Green",
            Price = data.VariantPrice + 5m,
            Stock = 20,
        };
        dbContext.ProductVariants.Add(variant);
        await dbContext.SaveChangesAsync();
        return (variant.Id, variant.Sku, variant.SizeValue, variant.Price.Value);
    }

    public async Task<Guid> SeedAccountOrderAsync(E2ETestData data)
    {
        await using var db = CreateDbContext();
        var user = await db.Users.SingleAsync(candidate => candidate.Id == data.UserId);
        var order = new Order
        {
            UserId = data.UserId,
            Reference = $"UI-{data.SimpleProductSlug}",
            Currency = "EUR",
            CustomerNameSnapshot = user.FullName,
            CustomerEmailSnapshot = user.Email,
            OrderStatus = OrderStatus.Confirmed,
            PaymentStatus = OrderPaymentStatus.Paid,
            FulfillmentStatus = FulfillmentStatus.Shipped,
            SubtotalAmount = data.VariantPrice * 2,
            TotalAmount = data.VariantPrice * 2,
            Lines = [new OrderLine
            {
                ProductId = data.VariantProductId,
                ProductVariantId = data.VariantId,
                ProductNameSnapshot = data.VariantProductName,
                SkuSnapshot = data.VariantSku,
                SizeScaleSnapshot = "ShoesUS",
                SizeValueSnapshot = data.VariantSize,
                ColorSnapshot = "Blue",
                Quantity = 2,
                UnitPrice = data.VariantPrice,
                LineTotal = data.VariantPrice * 2,
            }],
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    public async Task GrantScenarioAdminAsync(E2ETestData data)
    {
        await using var db = CreateDbContext();
        var role = await db.Roles.SingleAsync(candidate => candidate.Name == "Admin");
        var customerRoles = await db.UserRoles.Where(candidate => candidate.UserId == data.UserId).ToListAsync();
        db.UserRoles.RemoveRange(customerRoles);
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = data.UserId, RoleId = role.Id });
        await db.SaveChangesAsync();
    }

    public async Task WithAdminDatabaseAsync(Func<AppDbContext, Task> action)
    {
        await using var db = CreateDbContext();
        await action(db);
    }

    public async Task RemoveScenarioAsync(E2ETestData data)
    {
        await using var dbContext = CreateDbContext();
        dbContext.AdminAuditLogs.RemoveRange(await dbContext.AdminAuditLogs.Where(entry => entry.ActorUserId == data.UserId).ToListAsync());
        var orders = await dbContext.Orders.Where(order => order.UserId == data.UserId).ToListAsync();
        dbContext.Orders.RemoveRange(orders);
        var user = await dbContext.Users.SingleOrDefaultAsync(candidate => candidate.Id == data.UserId);
        if (user is not null)
        {
            dbContext.Users.Remove(user);
        }

        var products = await dbContext.Products
            .Where(product => product.CategoryId == data.CategoryId)
            .ToListAsync();
        dbContext.Products.RemoveRange(products);

        var category = await dbContext.Categories.SingleOrDefaultAsync(candidate => candidate.Id == data.CategoryId);
        if (category is not null)
        {
            dbContext.Categories.Remove(category);
        }

        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (application is null)
        {
            return;
        }

        try
        {
            await application.StopAsync();
        }
        catch (ObjectDisposedException)
        {
            // Startup failures can dispose the Aspire service provider before fixture cleanup runs.
        }
        finally
        {
            await application.DisposeAsync();
        }
    }

    private AppDbContext CreateDbContext()
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("The E2E PostgreSQL connection is not initialized.");
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;
        return new AppDbContext(options);
    }

    private static async Task WaitForHttpReadyAsync(Uri baseUri)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };
        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(5),
        };
        using var timeout = new CancellationTokenSource(StartupTimeout);

        while (!timeout.IsCancellationRequested)
        {
            try
            {
                using var response = await client.GetAsync(baseUri, timeout.Token);
                if ((int)response.StatusCode < 500)
                {
                    return;
                }
            }
            catch (HttpRequestException) when (!timeout.IsCancellationRequested)
            {
            }
            catch (TaskCanceledException) when (!timeout.IsCancellationRequested)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
        }

        throw new TimeoutException($"The application endpoint {baseUri} did not become ready within {StartupTimeout}.");
    }

    private static string ResolveArtifactDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("BLAZORSHOP_E2E_ARTIFACTS_DIR");
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, "e2e-artifacts")
            : Path.GetFullPath(configured);
    }
}
