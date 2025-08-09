
namespace BlazorShop.API;

using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BlazorShop.Application;
using BlazorShop.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Configure Serilog from configuration with enrichers
        builder.Host.UseSerilog((context, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithProcessId()
                .Enrich.WithThreadId()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("ApplicationName", "BlazorShop.API");
        });

        Log.Logger.Information("Application Starting...");

        // Configure JSON serialization
        builder.Services.AddControllers().AddJsonOptions(opt =>
            opt.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

        // OpenAPI/Swagger
        builder.Services.AddSwaggerGen();

        // Service defaults (includes OpenTelemetry, HealthChecks, Service Discovery)
        builder.AddServiceDefaults();

        // Add infrastructure and application services
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddApplication();

        // Configure CORS with security-focused settings
        ConfigureCors(builder.Services, builder.Configuration);

        // Configure rate limiting
        ConfigureRateLimiting(builder.Services, builder.Configuration);

        // Configure additional health checks
        ConfigureHealthChecks(builder.Services, builder.Configuration);

        try
        {
            var app = builder.Build();

            // Configure the HTTP request pipeline
            ConfigureMiddleware(app);

            Log.Logger.Information("Application Started");
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Logger.Fatal(ex, "The application failed to start correctly.");
            throw;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureCors(IServiceCollection services, IConfiguration configuration)
    {
        var securityConfig = configuration.GetSection("Security");
        var enableCorsPolicy = securityConfig.GetValue<bool>("EnableCorsPolicy");
        var allowedOrigins = securityConfig.GetSection("AllowedOrigins").Get<string[]>() ?? [];

        if (enableCorsPolicy)
        {
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    if (allowedOrigins.Length > 0)
                    {
                        policy.WithOrigins(allowedOrigins);
                    }
                    else
                    {
                        // Development fallback - allow localhost origins
                        policy.SetIsOriginAllowed(origin =>
                            origin.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) ||
                            origin.StartsWith("https://localhost", StringComparison.OrdinalIgnoreCase));
                    }

                    policy.AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                });
            });
        }
    }

    private static void ConfigureRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        var enableRateLimiting = configuration.GetSection("Security").GetValue<bool>("EnableRateLimiting");

        if (enableRateLimiting)
        {
            var rateLimitConfig = configuration.GetSection("RateLimiting");
            var globalPolicy = rateLimitConfig.GetSection("GlobalPolicy");
            var apiPolicy = rateLimitConfig.GetSection("ApiPolicy");

            services.AddRateLimiter(options =>
            {
                // Global rate limiting policy
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetTokenBucketLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new TokenBucketRateLimiterOptions
                        {
                            TokenLimit = globalPolicy.GetValue<int>("PermitLimit", 100),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = globalPolicy.GetValue<int>("QueueLimit", 10),
                            ReplenishmentPeriod = globalPolicy.GetValue<TimeSpan>("ReplenishmentPeriod", TimeSpan.FromSeconds(1)),
                            TokensPerPeriod = globalPolicy.GetValue<int>("TokensPerPeriod", 1),
                            AutoReplenishment = globalPolicy.GetValue<bool>("AutoReplenishment", true)
                        }));

                // API-specific rate limiting policy
                options.AddPolicy("ApiPolicy", context =>
                    RateLimitPartition.GetTokenBucketLimiter(
                        partitionKey: $"{context.Connection.RemoteIpAddress}-api",
                        factory: _ => new TokenBucketRateLimiterOptions
                        {
                            TokenLimit = apiPolicy.GetValue<int>("PermitLimit", 50),
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = apiPolicy.GetValue<int>("QueueLimit", 5),
                            ReplenishmentPeriod = apiPolicy.GetValue<TimeSpan>("ReplenishmentPeriod", TimeSpan.FromSeconds(1)),
                            TokensPerPeriod = apiPolicy.GetValue<int>("TokensPerPeriod", 1),
                            AutoReplenishment = apiPolicy.GetValue<bool>("AutoReplenishment", true)
                        }));

                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter = 
                            ((int)retryAfter.TotalSeconds).ToString(System.Globalization.NumberFormatInfo.InvariantInfo);
                    }

                    await context.HttpContext.Response.WriteAsync("Rate limit exceeded. Try again later.", token);
                };
            });
        }
    }

    private static void ConfigureHealthChecks(IServiceCollection services, IConfiguration configuration)
    {
        var healthCheckConfig = configuration.GetSection("HealthChecks");
        var enableDatabaseCheck = healthCheckConfig.GetValue<bool>("EnableDatabaseCheck", true);
        var dbTimeout = healthCheckConfig.GetValue<TimeSpan>("DatabaseCheckTimeout", TimeSpan.FromSeconds(5));

        var healthChecksBuilder = services.AddHealthChecks();

        // Add database health check if enabled and connection string is available
        if (enableDatabaseCheck)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                healthChecksBuilder.AddSqlServer(
                    connectionString: connectionString,
                    name: "database",
                    timeout: dbTimeout,
                    tags: ["ready", "db"]);
            }
        }
    }

    private static void ConfigureMiddleware(WebApplication app)
    {
        // Use CORS
        app.UseCors();

        // Serilog request logging
        app.UseSerilogRequestLogging(options =>
        {
            options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            options.GetLevel = (httpContext, elapsed, ex) => ex != null 
                ? Serilog.Events.LogEventLevel.Error 
                : httpContext.Response.StatusCode > 499 
                    ? Serilog.Events.LogEventLevel.Error
                    : Serilog.Events.LogEventLevel.Information;
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
                diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault());
                diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
            };
        });

        // Security headers
        var securityConfig = app.Configuration.GetSection("Security");
        var requireHttps = securityConfig.GetValue<bool>("RequireHttps", true);
        var enableHsts = securityConfig.GetValue<bool>("EnableHsts", true);

        if (requireHttps)
        {
            app.UseHttpsRedirection();
        }

        if (enableHsts && app.Environment.IsProduction())
        {
            app.UseHsts();
        }

        // Static files
        app.UseStaticFiles();

        // Rate limiting
        if (securityConfig.GetValue<bool>("EnableRateLimiting"))
        {
            app.UseRateLimiter();
        }

        // Infrastructure middleware (includes exception handling)
        app.UseInfrastructure();

        // Development-specific middleware
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // Authentication & Authorization
        app.UseAuthentication();
        app.UseAuthorization();

        // Health checks endpoints
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live")
        });

        // Map service defaults endpoints (includes /health endpoint)
        app.MapDefaultEndpoints();

        // Map controllers with rate limiting on API routes
        if (securityConfig.GetValue<bool>("EnableRateLimiting"))
        {
            app.MapControllers().RequireRateLimiting("ApiPolicy");
        }
        else
        {
            app.MapControllers();
        }
    }
}
