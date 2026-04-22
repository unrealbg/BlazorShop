using System.IO;

using BlazorShop.Application.Diagnostics;
using BlazorShop.Application.Options;
using BlazorShop.Application.Services;
using BlazorShop.Application.Services.Contracts;
using BlazorShop.Storefront.Options;
using BlazorShop.Storefront;
using BlazorShop.Storefront.Services;
using BlazorShop.Storefront.Services.Contracts;

using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.Configure<ClientAppOptions>(builder.Configuration.GetSection(ClientAppOptions.SectionName));
builder.Services.Configure<StorefrontPublicUrlOptions>(builder.Configuration.GetSection(StorefrontPublicUrlOptions.SectionName));
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
});
builder.Services.AddHttpClient<StorefrontApiClient>((serviceProvider, client) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    client.BaseAddress = ResolveApiBaseAddress(configuration);
});

var app = builder.Build();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = CreateStaticFileProvider(app.Environment),
});
app.UseMiddleware<StorefrontPublicRedirectMiddleware>();
app.Use(async (context, next) =>
{
    StorefrontResponseHeaders.RegisterErrorStatusHeaders(context);
    await next();
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
    var configuredBaseAddress = configuration["Api:BaseUrl"];
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