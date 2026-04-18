using System.IO;

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
builder.Services.Configure<StorefrontPublicUrlOptions>(builder.Configuration.GetSection(StorefrontPublicUrlOptions.SectionName));
builder.Services.AddRazorComponents();
builder.Services.AddSingleton<ISeoMetadataBuilder, SeoMetadataBuilder>();
builder.Services.AddScoped<IStorefrontPublicUrlResolver, StorefrontPublicUrlResolver>();
builder.Services.AddScoped<IStorefrontRobotsService, StorefrontRobotsService>();
builder.Services.AddScoped<IStorefrontSeoSettingsProvider, StorefrontSeoSettingsProvider>();
builder.Services.AddScoped<IStorefrontSeoComposer, StorefrontSeoComposer>();
builder.Services.AddScoped<IStorefrontStructuredDataComposer, StorefrontStructuredDataComposer>();
builder.Services.AddScoped<IStorefrontSitemapService, StorefrontSitemapService>();
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
app.UseAntiforgery();
app.MapDefaultEndpoints();
app.MapGet(StorefrontRoutes.Robots, async (IStorefrontRobotsService robotsService, CancellationToken cancellationToken) =>
{
    var content = await robotsService.GenerateAsync(cancellationToken);
    return Results.Text(content, "text/plain; charset=utf-8");
});
app.MapGet(StorefrontRoutes.Sitemap, async (IStorefrontSitemapService sitemapService, CancellationToken cancellationToken) =>
{
    var result = await sitemapService.GenerateAsync(cancellationToken);
    return result.IsServiceUnavailable || string.IsNullOrWhiteSpace(result.Content)
        ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable)
        : Results.Text(result.Content, "application/xml; charset=utf-8");
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

public partial class Program;