using System.IO;

using BlazorShop.Application.Diagnostics;
using BlazorShop.Application.Options;
using BlazorShop.Application.Services;
using BlazorShop.Application.Services.Contracts;
using BlazorShop.Storefront.Configuration;
using BlazorShop.Storefront.Options;
using BlazorShop.Storefront;
using BlazorShop.Storefront.Services;
using BlazorShop.Storefront.Services.Contracts;

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

// Configure the two credential-bearing clients explicitly; API defaults stay unchanged.
builder.AddServiceDefaults(configureHttpResilience: false);

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection(DemoOptions.SectionName));
builder.Services.AddOptions<SessionObservationOptions>()
    .Bind(builder.Configuration.GetSection(SessionObservationOptions.SectionName))
    .Validate(options => !options.Enabled || options.HasValidKey(), "SessionObservation requires a 32-byte base64 service key when enabled.")
    .Validate(options => !options.AllowInsecureDevelopmentTransport || builder.Environment.IsDevelopment(), "Insecure observation transport is Development-only.")
    .ValidateOnStart();
builder.Services.AddTransient<StorefrontDemoSessionHandler>();
builder.Services.AddTransient<SessionObservationCredentialHandler>();
builder.Services.AddSingleton<IValidateOptions<StorefrontApiOptions>, StorefrontApiOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<ClientAppOptions>, StorefrontClientAppOptionsValidator>();
builder.Services.AddSingleton<IValidateOptions<StorefrontPublicUrlOptions>, StorefrontPublicUrlOptionsValidator>();
builder.Services.AddOptions<StorefrontApiOptions>()
    .Bind(builder.Configuration.GetSection(StorefrontApiOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<ClientAppOptions>()
    .Bind(builder.Configuration.GetSection(ClientAppOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddOptions<StorefrontPublicUrlOptions>()
    .Bind(builder.Configuration.GetSection(StorefrontPublicUrlOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddRazorComponents();
builder.Services.AddSingleton<ISeoMetadataBuilder, SeoMetadataBuilder>();
builder.Services.AddScoped<IStorefrontClientAppUrlResolver, StorefrontClientAppUrlResolver>();
builder.Services.AddScoped<IStorefrontPublicUrlResolver, StorefrontPublicUrlResolver>();
builder.Services.AddScoped<IStorefrontRobotsService, StorefrontRobotsService>();
builder.Services.AddScoped<IStorefrontSeoSettingsProvider, StorefrontSeoSettingsProvider>();
builder.Services.AddScoped<IStorefrontSeoComposer, StorefrontSeoComposer>();
builder.Services.AddScoped<IStorefrontStructuredDataComposer, StorefrontStructuredDataComposer>();
builder.Services.AddScoped<IStorefrontSitemapService, StorefrontSitemapService>();
builder.Services.AddHttpClient<IStorefrontSessionResolver, StorefrontSessionResolver>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    client.BaseAddress = ResolveApiBaseAddress(configuration);
    client.Timeout = TimeSpan.FromSeconds(2);
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false })
  .RedactLoggedHeaders(_ => true)
  .AddHttpMessageHandler<StorefrontDemoSessionHandler>()
  .AddHttpMessageHandler<SessionObservationCredentialHandler>();
builder.Services.AddHttpClient<StorefrontApiClient>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    client.BaseAddress = ResolveApiBaseAddress(configuration);
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { UseCookies = false, AllowAutoRedirect = false })
  .RedactLoggedHeaders(_ => true)
  .AddHttpMessageHandler<StorefrontDemoSessionHandler>()
  .AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

var app = builder.Build();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = CreateStaticFileProvider(app.Environment),
});
app.Use(async (context, next) =>
{
    var refreshName = app.Configuration["Api:RefreshTokenCookieName"] ?? "__Host-blazorshop-refresh";
    var demoName = app.Services.GetRequiredService<IOptions<DemoOptions>>().Value.CookieName;
    if (context.Request.Cookies.ContainsKey(refreshName) || context.Request.Cookies.ContainsKey(demoName))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "private, no-store, no-cache, max-age=0";
            return Task.CompletedTask;
        });
    }
    await next();
});
app.UseMiddleware<StorefrontPublicRedirectMiddleware>();
app.Use(async (context, next) =>
{
    StorefrontResponseHeaders.RegisterErrorStatusHeaders(context);
    await next();
});
app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;
    var (title, heading, message) = response.StatusCode switch
    {
        StatusCodes.Status404NotFound => ("Page not found", "Page not found", "The page you requested could not be found."),
        StatusCodes.Status503ServiceUnavailable => ("Service unavailable", "Temporarily unavailable", "Please try again in a few minutes."),
        _ => ("Request error", "Request error", "The request could not be completed."),
    };

    response.ContentType = "text/html; charset=utf-8";
    await response.WriteAsync($$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <meta name="robots" content="noindex,nofollow">
          <title>{{title}} | BlazorShop</title>
        </head>
        <body>
          <main>
            <h1>{{heading}}</h1>
            <p>{{message}}</p>
            <p><a href="/">Return to the shop</a></p>
          </main>
        </body>
        </html>
        """);
});
app.UseAntiforgery();
app.MapDefaultEndpoints();
app.MapGet(StorefrontRoutes.SignIn, (IStorefrontClientAppUrlResolver clientAppUrlResolver) =>
    CreateClientRedirectResult(clientAppUrlResolver, "/authentication/login/account"));
app.MapGet(StorefrontRoutes.Register, (IStorefrontClientAppUrlResolver clientAppUrlResolver) =>
    CreateClientRedirectResult(clientAppUrlResolver, "/authentication/register"));
app.MapGet(StorefrontRoutes.Checkout, async (HttpContext httpContext, IStorefrontClientAppUrlResolver clientAppUrlResolver, IStorefrontSessionResolver sessionResolver, CancellationToken cancellationToken) =>
{
    StorefrontResponseHeaders.ApplyPrivatePage(httpContext);

    var session = await sessionResolver.GetCurrentUserAsync(cancellationToken);
    var targetPath = session.IsAuthenticated ? "/account/checkout" : "/authentication/login/account/checkout";
    return CreateClientRedirectResult(clientAppUrlResolver, targetPath);
});
app.MapGet(StorefrontRoutes.Robots, async (HttpContext httpContext, IStorefrontRobotsService robotsService, CancellationToken cancellationToken) =>
{
    try
    {
        var content = await robotsService.GenerateAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            SeoRuntimeLogger.PublicDiscoveryRobotsFailure(app.Logger, StorefrontRoutes.Robots, "empty_document");
            StorefrontResponseHeaders.ApplyServiceUnavailable(httpContext);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        StorefrontResponseHeaders.ApplyRobotsDocument(httpContext.Response);
        return Results.Text(content, "text/plain; charset=utf-8");
    }
    catch (Exception exception)
    {
        SeoRuntimeLogger.PublicDiscoveryRobotsFailure(app.Logger, exception, StorefrontRoutes.Robots, "generation_exception");
        StorefrontResponseHeaders.ApplyServiceUnavailable(httpContext);
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapGet(StorefrontRoutes.Sitemap, async (HttpContext httpContext, IStorefrontSitemapService sitemapService, CancellationToken cancellationToken) =>
{
    try
    {
        var result = await sitemapService.GenerateAsync(cancellationToken);
        if (result.IsServiceUnavailable)
        {
            SeoRuntimeLogger.PublicDiscoverySitemapFailure(app.Logger, StorefrontRoutes.Sitemap, "upstream_service_unavailable");
            StorefrontResponseHeaders.ApplySitemapUnavailable(httpContext.Response);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(result.Content))
        {
            SeoRuntimeLogger.PublicDiscoverySitemapFailure(app.Logger, StorefrontRoutes.Sitemap, "empty_document");
            StorefrontResponseHeaders.ApplySitemapUnavailable(httpContext.Response);
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        StorefrontResponseHeaders.ApplySitemapDocument(httpContext.Response);
        return Results.Text(result.Content, "application/xml; charset=utf-8");
    }
    catch (Exception exception)
    {
        SeoRuntimeLogger.PublicDiscoverySitemapFailure(app.Logger, exception, StorefrontRoutes.Sitemap, "generation_exception");
        StorefrontResponseHeaders.ApplySitemapUnavailable(httpContext.Response);
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
});
app.MapRazorComponents<App>();

app.Run();

static Uri ResolveApiBaseAddress(IConfiguration configuration)
{
    var configuredBaseAddress = configuration[$"{StorefrontApiOptions.SectionName}:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(configuredBaseAddress)
        && Uri.TryCreate(configuredBaseAddress, UriKind.Absolute, out var configuredUri))
    {
        return configuredUri;
    }

    return new Uri("https+http://apiservice/api/");
}

static IFileProvider CreateStaticFileProvider(IWebHostEnvironment environment)
{
    var fileProviders = new List<IFileProvider>
    {
        environment.WebRootFileProvider,
    };

    var sharedWebRootPath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "BlazorShop.Web", "wwwroot"));
    if (Directory.Exists(sharedWebRootPath))
    {
        fileProviders.Add(new PhysicalFileProvider(sharedWebRootPath));
    }

    return fileProviders.Count == 1
        ? fileProviders[0]
        : new CompositeFileProvider(fileProviders);
}

static IResult CreateClientRedirectResult(IStorefrontClientAppUrlResolver clientAppUrlResolver, string targetPath)
{
    if (string.IsNullOrWhiteSpace(clientAppUrlResolver.ResolveBaseUrl()))
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Redirect(clientAppUrlResolver.ResolveUrl(targetPath));
}

public partial class Program;
