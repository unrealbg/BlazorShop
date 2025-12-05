namespace BlazorShop.Infrastructure
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Logging;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Application.Services.Payment;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Contracts.CategoryPersistence;
    using BlazorShop.Domain.Contracts.Newsletters;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.ExceptionsMiddleware;
    using BlazorShop.Infrastructure.Repositories;
    using BlazorShop.Infrastructure.Repositories.Authentication;
    using BlazorShop.Infrastructure.Repositories.CategoryPersistence;
    using BlazorShop.Infrastructure.Repositories.Newsletters;
    using BlazorShop.Infrastructure.Repositories.Payment;
    using BlazorShop.Infrastructure.Services;

    using EntityFramework.Exceptions.PostgreSQL;

    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Diagnostics;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.IdentityModel.Tokens;

    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            services.AddDbContext<AppDbContext>(
                opt => opt
                    .UseNpgsql(
                        config.GetConnectionString("DefaultConnection"),
                        npgsqlOptions =>
                            {
                                npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                                npgsqlOptions.EnableRetryOnFailure();
                            })
                    .UseExceptionProcessor()
                    .ConfigureWarnings(w => w.Log(RelationalEventId.PendingModelChangesWarning))
            );

            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
            services.AddScoped(typeof(IAppLogger<>), typeof(LoggerAdapter<>));

            services.AddDefaultIdentity<AppUser>(
                opt =>
                    {
                        opt.SignIn.RequireConfirmedEmail = true;
                        opt.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;
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
            services.AddScoped<IPaymentService, StripePaymentService>();
            services.AddScoped<IPayPalPaymentService, PayPalPaymentService>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IOrderTrackingService, OrderTrackingService>();
            services.AddScoped<IOrderQueryService, OrderQueryService>();
            services.AddScoped<INewsletterSubscriberRepository, NewsletterSubscriberRepository>();

            services.AddScoped<ICategoryRepository, CategoryRepository>();

            services.AddScoped<ICart, CartRepository>();

            // Product recommendations
            services.AddScoped<IProductRecommendationRepository, ProductRecommendationRepository>();

            // Add memory cache for recommendations
            services.AddMemoryCache();

            Stripe.StripeConfiguration.ApiKey = config["Stripe:SecretKey"];

            services.Configure<EmailSettings>(config.GetSection("EmailSettings"));
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
