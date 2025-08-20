namespace BlazorShop.Web
{
    using BlazorShop.Web.Authentication.Providers;
    using BlazorShop.Web.Shared;
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

            builder.Services.AddScoped<IBrowserCookieStorageService, BrowserCookieStorageService>();
            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.AddScoped<ITokenService, TokenService>();
            builder.Services.AddScoped<IHttpClientHelper, HttpClientHelper>();
            builder.Services.AddScoped<IApiCallHelper, ApiCallHelper>();
            builder.Services.AddScoped<IProductService, ProductService>();
            builder.Services.AddScoped<ICategoryService, CategoryService>();
            builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
            builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
            builder.Services.AddScoped<RefreshTokenHandler>();
            builder.Services.AddHttpClient(
                Constant.ApiClient.Name,
                opt =>
                    {
                        opt.BaseAddress = new Uri("https://localhost:7094/api/");
                    }).AddHttpMessageHandler<RefreshTokenHandler>();
            builder.Services.AddCascadingAuthenticationState();
            builder.Services.AddAuthorizationCore();
            builder.Services.AddScoped<ICartService, CartService>();
            builder.Services.AddScoped<IPaymentMethodService, PaymentMethodService>();
            builder.Services.AddScoped<IFileUploadService, FileUploadService>();
            builder.Services.AddScoped<IProductVariantService, ProductVariantService>();
            builder.Services.AddSingleton<IToastService, ToastService>();
            builder.Services.AddScoped<INewsletterService, NewsletterService>();

            await builder.Build().RunAsync();
        }
    }
}
