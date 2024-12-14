namespace BlazorShop.Infrastructure
{
    using BlazorShop.Application.Services.Contracts.Logging;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Contracts.CategoryPersistence;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.ExceptionsMiddleware;
    using BlazorShop.Infrastructure.Repositories;
    using BlazorShop.Infrastructure.Repositories.Authentication;
    using BlazorShop.Infrastructure.Repositories.CategoryPersistence;
    using BlazorShop.Infrastructure.Repositories.Payment;
    using BlazorShop.Infrastructure.Services;

    using EntityFramework.Exceptions.SqlServer;

    using Microsoft.AspNetCore.Authentication.JwtBearer;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.IdentityModel.Tokens;

    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            services.AddDbContext<AppDbContext>(
                opt => opt.UseSqlServer(
                    config.GetConnectionString("DefaultConnection"),
                    sqlOptions =>
                        {
                            sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                            sqlOptions.EnableRetryOnFailure();
                        }).UseExceptionProcessor());

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

            services.AddScoped<ICategoryRepository, CategoryRepository>();

            Stripe.StripeConfiguration.ApiKey = config["Stripe:SecretKey"];

            return services;
        }

        public static IApplicationBuilder UseInfrastructure(this IApplicationBuilder app)
        {
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            return app;
        }
    }
}
