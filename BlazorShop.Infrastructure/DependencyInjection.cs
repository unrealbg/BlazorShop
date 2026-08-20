namespace BlazorShop.Infrastructure
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Application.Services.Contracts.Admin;
    using BlazorShop.Application.Services.Contracts.Logging;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Application.Services.Payment;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Contracts.CategoryPersistence;
    using BlazorShop.Domain.Contracts.Newsletters;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Contracts.Seo;
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Infrastructure.Configuration;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Demo;
    using BlazorShop.Infrastructure.ExceptionsMiddleware;
    using BlazorShop.Infrastructure.Repositories;
    using BlazorShop.Infrastructure.Repositories.Authentication;
    using BlazorShop.Infrastructure.Repositories.CategoryPersistence;
    using BlazorShop.Infrastructure.Repositories.Newsletters;
    using BlazorShop.Infrastructure.Repositories.Payment;
    using BlazorShop.Infrastructure.Repositories.Seo;
    using BlazorShop.Infrastructure.Services;
    using BlazorShop.Infrastructure.Services.Admin;

    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using Microsoft.IdentityModel.Tokens;

    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            services.AddOptions<DemoOptions>()
                .Bind(config.GetSection(DemoOptions.SectionName))
                .Validate(options => options.LifetimeMinutes is >= 5 and <= 120, "Demo:LifetimeMinutes must be between 5 and 120.")
                .Validate(options => options.MaxConcurrentSessions is >= 1 and <= 100, "Demo:MaxConcurrentSessions must be between 1 and 100.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.CookieName), "Demo:CookieName is required.")
                .Validate(options => !string.IsNullOrWhiteSpace(options.HeaderName), "Demo:HeaderName is required.")
                .Validate(options => !options.Enabled
                    || (!string.IsNullOrWhiteSpace(options.CustomerEmail)
                        && !string.IsNullOrWhiteSpace(options.AdminEmail)
                        && !string.IsNullOrWhiteSpace(options.Password)),
                    "Demo credentials are required when demo mode is enabled.")
                .ValidateOnStart();

            services.AddSingleton<DemoRequestContext>();
            services.AddSingleton<BlazorShop.Application.Services.Contracts.Demo.IDemoRequestContext>(
                serviceProvider => serviceProvider.GetRequiredService<DemoRequestContext>());
            services.AddSingleton<IDemoSessionManager, DemoSessionManager>();
            services.AddHostedService<DemoSessionCleanupService>();

            services.AddDbContext<AppDbContext>(
                (serviceProvider, options) =>
                {
                    var demoSession = serviceProvider.GetRequiredService<DemoRequestContext>().Current;

                    if (demoSession is not null)
                    {
                        options.UseNpgsql(
                            demoSession.Connection,
                            false,
                            npgsqlOptions => npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName));
                        return;
                    }

                    options.UseNpgsql(
                        config.GetConnectionString("DefaultConnection"),
                        npgsqlOptions =>
                        {
                            npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                            npgsqlOptions.EnableRetryOnFailure();
                        });
                });

            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped<IProductReadRepository, ProductReadRepository>();
            services.AddScoped(typeof(IAppLogger<>), typeof(LoggerAdapter<>));

            services.AddDefaultIdentity<AppUser>(
                opt =>
                    {
                        opt.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
                        opt.Lockout.AllowedForNewUsers = true;
                        opt.Lockout.MaxFailedAccessAttempts = 5;
                        opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                        opt.Password.RequireDigit = true;
                        opt.Password.RequireNonAlphanumeric = true;
                        opt.Password.RequiredLength = 8;
                        opt.Password.RequireLowercase = true;
                        opt.Password.RequireUppercase = true;
                        opt.Password.RequiredUniqueChars = 1;
                    })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<AppDbContext>();

            services.AddAuthentication(opt =>
                {
                    opt.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    opt.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    opt.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                }).AddJwtBearer(opt =>
                {
                    opt.SaveToken = true;
                    opt.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        ValidateIssuerSigningKey = true,
                        ValidAudience = config["JWT:Audience"],
                        ValidIssuer = config["JWT:Issuer"],
                        ClockSkew = TimeSpan.Zero,
                        IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(config["JWT:Key"]!)),
                    };
                });

            services.AddScoped<IAppUserManager, AppUserManager>();
            services.AddScoped<IAppTokenManager, AppTokenManager>();
            services.AddScoped<IAppRoleManager, AppRoleManager>();

            services.AddScoped<IPaymentMethod, PaymentMethodRepository>();
            services.AddScoped<IStripeCheckoutSessionService, StripeCheckoutSessionService>();
            services.AddScoped<IPaymentService, StripePaymentService>();
            services.AddSingleton<IStripeWebhookEventParser, StripeWebhookEventParser>();
            services.AddScoped<IStripeWebhookService, StripeWebhookService>();
            services.AddScoped<IPayPalPaymentService, PayPalPaymentService>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IInventoryReservationService, InventoryReservationService>();
            services.AddScoped<IOrderTrackingService, OrderTrackingService>();
            services.AddScoped<IOrderQueryService, OrderQueryService>();
            services.AddScoped<INewsletterSubscriberRepository, NewsletterSubscriberRepository>();
            services.AddScoped<ISeoSettingsRepository, SeoSettingsRepository>();
            services.AddScoped<ISeoRedirectRepository, SeoRedirectRepository>();
            services.AddScoped<IApplicationTransactionManager, ApplicationTransactionManager>();
            services.AddHttpContextAccessor();
            services.AddScoped<IAdminAuditService, AdminAuditService>();
            services.AddScoped<IAdminUserService, AdminUserService>();
            services.AddScoped<IAdminSettingsService, AdminSettingsService>();
            services.AddScoped<IAdminInventoryService, AdminInventoryService>();
            services.AddScoped<IAdminOrderService, AdminOrderService>();

            services.AddScoped<ICategoryRepository, CategoryRepository>();

            services.AddScoped<ICart, CartRepository>();

            // Product recommendations
            services.AddScoped<IProductRecommendationRepository, ProductRecommendationRepository>();

            // Add memory cache for recommendations
            services.AddMemoryCache();

            services.AddOptions<StripeOptions>()
                .Bind(config.GetSection(StripeOptions.SectionName));

            Stripe.StripeConfiguration.ApiKey = config[$"{StripeOptions.SectionName}:SecretKey"];

            services.AddSingleton<IValidateOptions<EmailSettings>, EmailSettingsOptionsValidator>();
            services.AddOptions<EmailSettings>()
                .Bind(config.GetSection("EmailSettings"))
                .ValidateOnStart();
            services.Configure<BankTransferSettings>(config.GetSection("BankTransfer"));
            services.AddTransient<IEmailService, EmailService>();

            return services;
        }

        public static IApplicationBuilder UseInfrastructure(this IApplicationBuilder app)
        {
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            return app;
        }
    }
}
