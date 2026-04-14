namespace BlazorShop.API
{
    using System.Net;
    using System.IO;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.RateLimiting;

    using BlazorShop.API.HealthChecks;
    using BlazorShop.API.Options;
    using BlazorShop.Application;
    using BlazorShop.Application.Options;
    using BlazorShop.Infrastructure;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Connections;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Diagnostics.HealthChecks;
    using Microsoft.AspNetCore.HttpOverrides;
    using Microsoft.AspNetCore.RateLimiting;
    using Microsoft.AspNetCore.StaticFiles;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Diagnostics.HealthChecks;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Hosting;

    using Serilog;

    public class Program
    {
        private const string ClientCorsPolicyName = "ClientOrigins";
        private const string PublicApiRateLimitPolicyName = "PublicApi";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var runtimeSection = builder.Configuration.GetSection(ApiRuntimeOptions.SectionName);
            var runtimeOptions = runtimeSection.Get<ApiRuntimeOptions>() ?? new ApiRuntimeOptions();
            var allowedCorsOrigins = ResolveAllowedCorsOrigins(runtimeOptions, builder.Configuration);

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .WriteTo.File("log/log.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            builder.Host.UseSerilog();

            Log.Logger.Information("Application Starting...");

            builder.Services.AddOptions<ApiRuntimeOptions>()
                .Bind(runtimeSection)
                .Validate(
                    options => options.Cors.AllowedOrigins.All(IsValidOrigin),
                    $"{ApiRuntimeOptions.SectionName}:Cors:AllowedOrigins must contain absolute URLs.")
                .Validate(
                    options => !options.ForwardedHeaders.Enabled
                        || options.ForwardedHeaders.KnownProxies.Length > 0
                        || options.ForwardedHeaders.KnownNetworks.Length > 0,
                    $"{ApiRuntimeOptions.SectionName}:ForwardedHeaders requires KnownProxies or KnownNetworks when enabled.")
                .Validate(
                    options => options.ForwardedHeaders.KnownProxies.All(IsValidIpAddress),
                    $"{ApiRuntimeOptions.SectionName}:ForwardedHeaders:KnownProxies must contain valid IP addresses.")
                .Validate(
                    options => options.ForwardedHeaders.KnownNetworks.All(IsValidCidrNotation),
                    $"{ApiRuntimeOptions.SectionName}:ForwardedHeaders:KnownNetworks must contain valid CIDR entries.")
                .Validate(
                    options => IsValidPath(options.Health.ReadyPath)
                        && IsValidPath(options.Health.LivePath)
                        && !string.Equals(options.Health.ReadyPath, options.Health.LivePath, StringComparison.OrdinalIgnoreCase),
                    $"{ApiRuntimeOptions.SectionName}:Health paths must start with '/' and be distinct.")
                .Validate(
                    options => options.RateLimiting.PermitLimit > 0
                        && options.RateLimiting.WindowSeconds > 0
                        && options.RateLimiting.QueueLimit >= 0,
                    $"{ApiRuntimeOptions.SectionName}:RateLimiting values must be positive.")
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.Security.RefreshTokenCookieName)
                        && IsValidSameSiteMode(options.Security.RefreshTokenCookieSameSite),
                    $"{ApiRuntimeOptions.SectionName}:Security refresh token cookie settings are invalid.")
                .ValidateOnStart();

            builder.Services.AddControllers().AddJsonOptions(opt =>
                opt.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddSwaggerGen();

            builder.AddServiceDefaults();

            builder.Services.AddInfrastructure(builder.Configuration);
            builder.Services.AddApplication(builder.Configuration);
            builder.Services.AddHealthChecks()
                .AddCheck<AppDbContextHealthCheck>("database", tags: ["ready"]);
            builder.Services.AddCors(
                options =>
                {
                    options.AddPolicy(
                        ClientCorsPolicyName,
                        policy =>
                        {
                            policy.AllowAnyHeader()
                                .AllowAnyMethod()
                                .AllowCredentials()
                                .SetIsOriginAllowed(origin => IsOriginAllowed(origin, allowedCorsOrigins, builder.Environment));
                        });
                });

            if (runtimeOptions.RateLimiting.Enabled)
            {
                builder.Services.AddRateLimiter(options => ConfigureRateLimiter(options, runtimeOptions.RateLimiting));
            }

            try
            {
                var app = builder.Build();

                if (!app.Environment.IsDevelopment() && runtimeOptions.ForwardedHeaders.Enabled)
                {
                    var forwardedHeadersOptions = new ForwardedHeadersOptions
                    {
                        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
                        ForwardLimit = runtimeOptions.ForwardedHeaders.ForwardLimit,
                    };

                    foreach (var proxy in runtimeOptions.ForwardedHeaders.KnownProxies)
                    {
                        forwardedHeadersOptions.KnownProxies.Add(IPAddress.Parse(proxy));
                    }

                    foreach (var network in runtimeOptions.ForwardedHeaders.KnownNetworks)
                    {
                        if (TryParseCidr(network, out var parsedNetwork))
                        {
                            forwardedHeadersOptions.KnownIPNetworks.Add(parsedNetwork);
                        }
                    }

                    app.UseForwardedHeaders(forwardedHeadersOptions);
                }

                DatabaseMigrationBootstrapper.MigrateAsync(app.Services).GetAwaiter().GetResult();

                if (app.Environment.IsDevelopment())
                {
                    using var scope = app.Services.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    DevelopmentCatalogSeeder.SeedAsync(dbContext).GetAwaiter().GetResult();
                }

                if (!app.Environment.IsDevelopment() && runtimeOptions.Security.EnableHsts)
                {
                    app.UseHsts();
                }

                if (runtimeOptions.Security.EnableHttpsRedirection)
                {
                    app.UseHttpsRedirection();
                }

                app.UseCors(ClientCorsPolicyName);

                if (runtimeOptions.RateLimiting.Enabled)
                {
                    app.UseRateLimiter();
                }

                app.UseSerilogRequestLogging();

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }

                var uploadsPath = Path.Combine(builder.Environment.ContentRootPath, "uploads");
                Directory.CreateDirectory(uploadsPath);

                Log.Logger.Information("ContentRootPath: {ContentRoot}", builder.Environment.ContentRootPath);
                Log.Logger.Information("Uploads path configured: {UploadsPath}", uploadsPath);

                app.UseStaticFiles(CreateUploadsStaticFileOptions(uploadsPath));

                app.UseInfrastructure();

                app.UseAuthentication();
                app.UseAuthorization();

                var controllerEndpoints = app.MapControllers();

                if (runtimeOptions.RateLimiting.Enabled)
                {
                    ApplyPublicApiRateLimiting(controllerEndpoints);
                }

                MapHealthEndpoints(app, runtimeOptions.Health);

                LogRuntimeConfiguration(app, runtimeOptions, allowedCorsOrigins);

                Log.Logger.Information("Application Started");

                app.Run();
            }
            catch (IOException e) when (e.InnerException is AddressInUseException || e.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
            {
                Log.Logger.Fatal(
                    e,
                    "The API port is already in use. Stop the existing API/AppHost instance or run only one launch mode at a time.");

                Environment.ExitCode = 1;
            }
            catch (Exception e)
            {
                Log.Logger.Fatal(e, "The application failed to start correctly.");
                Environment.ExitCode = 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void ConfigureRateLimiter(RateLimiterOptions options, PublicApiRateLimitingOptions rateLimitOptions)
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/json";

                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(new { message = "Too many requests. Please try again later." }),
                    cancellationToken);
            };

            options.AddPolicy(
                PublicApiRateLimitPolicyName,
                httpContext =>
                {
                    var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: clientIp,
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = rateLimitOptions.PermitLimit,
                            Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                            QueueLimit = rateLimitOptions.QueueLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            AutoReplenishment = true,
                        });
                });
        }

        private static void ApplyPublicApiRateLimiting(ControllerActionEndpointConventionBuilder controllerEndpoints)
        {
            controllerEndpoints.Add(
                endpointBuilder =>
                {
                    var hasAuthorization = endpointBuilder.Metadata.OfType<IAuthorizeData>().Any();

                    if (!hasAuthorization)
                    {
                        endpointBuilder.Metadata.Add(new EnableRateLimitingAttribute(PublicApiRateLimitPolicyName));
                    }
                });
        }

        private static void MapHealthEndpoints(WebApplication app, ApiHealthEndpointOptions healthOptions)
        {
            if (!app.Environment.IsDevelopment() && !healthOptions.ExposeInProduction)
            {
                return;
            }

            app.MapHealthChecks(
                healthOptions.ReadyPath,
                new HealthCheckOptions
                {
                    Predicate = _ => true,
                    ResponseWriter = WriteHealthResponseAsync,
                });

            app.MapHealthChecks(
                healthOptions.LivePath,
                new HealthCheckOptions
                {
                    Predicate = registration => registration.Tags.Contains("live"),
                    ResponseWriter = WriteHealthResponseAsync,
                });
        }

        private static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
        {
            context.Response.ContentType = "application/json";

            return context.Response.WriteAsync(
                JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                }));
        }

        private static void LogRuntimeConfiguration(
            WebApplication app,
            ApiRuntimeOptions runtimeOptions,
            HashSet<string> allowedCorsOrigins)
        {
            var configuredOrigins = allowedCorsOrigins.Count == 0
                ? "same-origin only"
                : string.Join(", ", allowedCorsOrigins.OrderBy(origin => origin, StringComparer.OrdinalIgnoreCase));

            Log.Logger.Information("CORS allowed origins: {Origins}", configuredOrigins);
            Log.Logger.Information(
                "Public API rate limiting enabled: {Enabled} (limit: {PermitLimit} requests / {WindowSeconds}s)",
                runtimeOptions.RateLimiting.Enabled,
                runtimeOptions.RateLimiting.PermitLimit,
                runtimeOptions.RateLimiting.WindowSeconds);
            Log.Logger.Information(
                "Health endpoints exposed: {Exposed}, ready path: {ReadyPath}, live path: {LivePath}",
                app.Environment.IsDevelopment() || runtimeOptions.Health.ExposeInProduction,
                runtimeOptions.Health.ReadyPath,
                runtimeOptions.Health.LivePath);
            Log.Logger.Information(
                "Forwarded headers enabled: {Enabled}",
                !app.Environment.IsDevelopment() && runtimeOptions.ForwardedHeaders.Enabled);
            Log.Logger.Information(
                "HSTS enabled: {Enabled}",
                !app.Environment.IsDevelopment() && runtimeOptions.Security.EnableHsts);
            Log.Logger.Information(
                "HTTPS redirection enabled: {Enabled}",
                runtimeOptions.Security.EnableHttpsRedirection);
            Log.Logger.Information(
                "Refresh token cookie same-site mode: {SameSiteMode}",
                runtimeOptions.Security.RefreshTokenCookieSameSite);
        }

        private static StaticFileOptions CreateUploadsStaticFileOptions(string uploadsPath)
        {
            var contentTypeProvider = new FileExtensionContentTypeProvider();
            contentTypeProvider.Mappings.Clear();
            contentTypeProvider.Mappings[".jpg"] = "image/jpeg";
            contentTypeProvider.Mappings[".jpeg"] = "image/jpeg";
            contentTypeProvider.Mappings[".png"] = "image/png";
            contentTypeProvider.Mappings[".webp"] = "image/webp";
            contentTypeProvider.Mappings[".gif"] = "image/gif";
            contentTypeProvider.Mappings[".bmp"] = "image/bmp";

            return new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(uploadsPath),
                RequestPath = "/uploads",
                ContentTypeProvider = contentTypeProvider,
                OnPrepareResponse = ctx =>
                {
                    ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
                    ctx.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                }
            };
        }

        private static HashSet<string> ResolveAllowedCorsOrigins(ApiRuntimeOptions runtimeOptions, IConfiguration configuration)
        {
            var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var origin in runtimeOptions.Cors.AllowedOrigins)
            {
                var normalizedOrigin = NormalizeOrigin(origin);
                if (normalizedOrigin is not null)
                {
                    allowedOrigins.Add(normalizedOrigin);
                }
            }

            var clientAppOrigin = NormalizeOrigin(configuration[$"{ClientAppOptions.SectionName}:BaseUrl"]);
            if (clientAppOrigin is not null)
            {
                allowedOrigins.Add(clientAppOrigin);
            }

            return allowedOrigins;
        }

        private static bool IsOriginAllowed(string origin, HashSet<string> allowedOrigins, IHostEnvironment environment)
        {
            var normalizedOrigin = NormalizeOrigin(origin);
            if (normalizedOrigin is null)
            {
                return false;
            }

            return allowedOrigins.Contains(normalizedOrigin)
                   || (environment.IsDevelopment() && IsLoopbackOrigin(normalizedOrigin));
        }

        private static string? NormalizeOrigin(string? origin)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                return null;
            }

            return uri.GetLeftPart(UriPartial.Authority);
        }

        private static bool IsLoopbackOrigin(string origin)
        {
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                return false;
            }

            return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                   || (IPAddress.TryParse(uri.Host, out var ipAddress) && IPAddress.IsLoopback(ipAddress));
        }

        private static bool IsValidOrigin(string origin)
        {
            return NormalizeOrigin(origin) is not null;
        }

        private static bool IsValidIpAddress(string proxy)
        {
            return IPAddress.TryParse(proxy, out _);
        }

        private static bool IsValidCidrNotation(string network)
        {
            return TryParseCidr(network, out _);
        }

        private static bool TryParseCidr(string cidr, out System.Net.IPNetwork parsedNetwork)
        {
            parsedNetwork = default!;

            if (string.IsNullOrWhiteSpace(cidr))
            {
                return false;
            }

            return System.Net.IPNetwork.TryParse(cidr, out parsedNetwork);
        }

        private static bool IsValidPath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && path.StartsWith('/');
        }

        private static bool IsValidSameSiteMode(string sameSiteMode)
        {
            return Enum.TryParse<SameSiteMode>(sameSiteMode, ignoreCase: true, out _);
        }
    }
}
