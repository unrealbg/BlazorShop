namespace BlazorShop.Web
{
    using BlazorShop.Web.Authentication.Providers;
    using BlazorShop.Web.Interop;
    using BlazorShop.Web.Services;
    using BlazorShop.Web.Services.Contracts;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.BrowserStorage;
    using BlazorShop.Web.Shared.BrowserStorage.Contracts;
    using BlazorShop.Web.Shared.CookieStorage;
    using BlazorShop.Web.Shared.CookieStorage.Contracts;
    using BlazorShop.Web.Shared.Helper;
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Services;
    using BlazorShop.Web.Shared.Services.Contracts;

    using Microsoft.AspNetCore.Components.Authorization;
    using Microsoft.AspNetCore.Components.Web;
    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            var apiBaseAddress = await ResolveApiBaseAddressAsync(builder);

            builder.Services.AddSingleton<IBrowserCookieStorageService, BrowserCookieStorageService>();
            builder.Services.AddSingleton<IBrowserSessionStorageService, BrowserSessionStorageService>();
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            // Authentication state is owned by this WASM tab, including factory handler scopes.
            builder.Services.AddSingleton<ITokenService, TokenService>();
            builder.Services.AddSingleton<IHttpClientHelper, HttpClientHelper>();
            builder.Services.AddSingleton<IApiCallHelper, ApiCallHelper>();
            builder.Services.AddScoped<IProductService, ProductService>();
            builder.Services.AddScoped<ICategoryService, CategoryService>();
            builder.Services.AddScoped<IProductSeoService, ProductSeoService>();
            builder.Services.AddScoped<ICategorySeoService, CategorySeoService>();
            builder.Services.AddScoped<ISeoSettingsService, SeoSettingsService>();
            builder.Services.AddScoped<ISeoRedirectService, SeoRedirectService>();
            builder.Services.AddScoped<IAdminUserService, AdminUserService>();
            builder.Services.AddScoped<IAdminSettingsService, AdminSettingsService>();
            builder.Services.AddScoped<IAdminAuditService, AdminAuditService>();
            builder.Services.AddScoped<IAdminInventoryService, AdminInventoryService>();
            builder.Services.AddScoped<IAdminOrderService, AdminOrderService>();
            builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
            builder.Services.AddSingleton<AuthenticationStateProvider, CustomAuthStateProvider>();
            builder.Services.AddSingleton<IAuthenticationStateNotifier, AuthenticationStateNotifier>();
            builder.Services.AddSingleton<IAuthenticatedClientStateCleaner, AuthenticatedClientStateCleaner>();
            builder.Services.AddBrowserSessionCoordination();
            builder.Services.AddScoped<IAuthenticationSessionBootstrapper, AuthenticationSessionBootstrapper>();
            builder.Services.AddScoped<IAuthenticationSessionEventPublisher, AuthenticationSessionEventPublisher>();
            builder.Services.AddScoped<IAuthenticationSessionSyncService, AuthenticationSessionSyncService>();
            builder.Services.AddSingleton<IStorefrontSeoMetadataBuilder, StorefrontSeoMetadataBuilder>();
            builder.Services.AddScoped<IStorefrontSeoService, StorefrontSeoService>();
            builder.Services.AddScoped<BrowserCredentialsHandler>();
            builder.Services.AddScoped<RefreshTokenHandler>();
            builder.Services.AddHttpClient(
                Constant.ApiClient.PublicName,
                client => { client.BaseAddress = apiBaseAddress; }
            ).AddHttpMessageHandler<BrowserCredentialsHandler>();
            builder.Services.AddHttpClient(
                Constant.ApiClient.PrivateName,
                client => { client.BaseAddress = apiBaseAddress; }
            ).AddHttpMessageHandler<RefreshTokenHandler>()
             .AddHttpMessageHandler<BrowserCredentialsHandler>();
            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddAuthorizationCore();
            builder.Services.AddScoped<ICartService, CartService>();
            builder.Services.AddScoped<ICheckoutAttemptStore, CheckoutAttemptStore>();
            builder.Services.AddScoped<IPaymentMethodService, PaymentMethodService>();
            builder.Services.AddScoped<IFileUploadService, FileUploadService>();
            builder.Services.AddScoped<IProductVariantService, ProductVariantService>();
            builder.Services.AddScoped<IProductRecommendationService, ProductRecommendationService>();
            builder.Services.AddSingleton<IToastService, ToastService>();
            builder.Services.AddSingleton<INotificationService, NotificationService>();
            builder.Services.AddSingleton<IPublicStorefrontUrlResolver, PublicStorefrontUrlResolver>();
            builder.Services.AddScoped<INewsletterService, NewsletterService>();
            builder.Services.AddScoped<IMetricsClient, MetricsClient>();
            builder.Services.AddScoped<IAppJsInterop, AppJsInterop>();
            builder.Services.AddScoped<IQueryFailureNotifier, QueryFailureNotifier>();

            var host = builder.Build();
            await host.Services.GetRequiredService<IAuthenticationSessionBootstrapper>().RestoreAsync();
            await host.RunAsync();
        }

        private static async Task<Uri> ResolveApiBaseAddressAsync(WebAssemblyHostBuilder builder)
        {
            var relativeApiBaseAddress = new Uri(new Uri(builder.HostEnvironment.BaseAddress), "api/");

            if (await IsRelativeApiAvailableAsync(relativeApiBaseAddress))
            {
                return relativeApiBaseAddress;
            }

            var configuredBaseAddress = builder.Configuration["Api:DirectBaseUrl"];
            if (!string.IsNullOrWhiteSpace(configuredBaseAddress) &&
                Uri.TryCreate(configuredBaseAddress, UriKind.Absolute, out var configuredUri))
            {
                return configuredUri;
            }

            return relativeApiBaseAddress;
        }

        private static async Task<bool> IsRelativeApiAvailableAsync(Uri relativeApiBaseAddress)
        {
            using var httpClient = new HttpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            try
            {
                using var response = await httpClient.GetAsync(new Uri(relativeApiBaseAddress, "swagger/v1/swagger.json"), cts.Token);
                var mediaType = response.Content.Headers.ContentType?.MediaType;

                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                return IsJsonMediaType(mediaType);
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private static bool IsJsonMediaType(string? mediaType)
        {
            if (string.IsNullOrWhiteSpace(mediaType))
            {
                return false;
            }

            return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
                   || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase);
        }
    }
}
