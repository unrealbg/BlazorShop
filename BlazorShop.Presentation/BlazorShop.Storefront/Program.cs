using System.IO;

using BlazorShop.Application.Services;
using BlazorShop.Application.Services.Contracts;
using BlazorShop.Storefront;
using BlazorShop.Storefront.Services;
using BlazorShop.Storefront.Services.Contracts;

using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddMemoryCache();
builder.Services.AddRazorComponents();
builder.Services.AddSingleton<ISeoMetadataBuilder, SeoMetadataBuilder>();
builder.Services.AddScoped<IStorefrontSeoSettingsProvider, StorefrontSeoSettingsProvider>();
builder.Services.AddScoped<IStorefrontSeoComposer, StorefrontSeoComposer>();
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
app.UseAntiforgery();
app.MapDefaultEndpoints();
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