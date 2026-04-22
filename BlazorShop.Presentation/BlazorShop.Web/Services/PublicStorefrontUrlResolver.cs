namespace BlazorShop.Web.Services
{
    using BlazorShop.Web.Services.Contracts;

    using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
    using Microsoft.Extensions.Configuration;

    public sealed class PublicStorefrontUrlResolver : IPublicStorefrontUrlResolver
    {
        private const string DevelopmentFallbackStorefrontUrl = "https://localhost:18597/";

        private readonly IConfiguration _configuration;
        private readonly IWebAssemblyHostEnvironment _hostEnvironment;

        public PublicStorefrontUrlResolver(IConfiguration configuration, IWebAssemblyHostEnvironment hostEnvironment)
        {
            _configuration = configuration;
            _hostEnvironment = hostEnvironment;
        }

        public string? Resolve()
        {
            return NormalizeBaseUrl(_configuration["PublicStorefront:BaseUrl"])
                   ?? NormalizeBaseUrl(_configuration["Services:storefront:https:0"])
                   ?? NormalizeBaseUrl(_configuration["Services:storefront:http:0"])
                   ?? ResolveDevelopmentFallback();
        }

        private string? ResolveDevelopmentFallback()
        {
            return _hostEnvironment.IsDevelopment()
                ? DevelopmentFallbackStorefrontUrl
                : null;
        }

        private static string? NormalizeBaseUrl(string? rawBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(rawBaseUrl))
            {
                return null;
            }

            if (!Uri.TryCreate(rawBaseUrl.Trim(), UriKind.Absolute, out var absoluteUri))
            {
                return null;
            }

            return absoluteUri.AbsoluteUri;
        }
    }
}