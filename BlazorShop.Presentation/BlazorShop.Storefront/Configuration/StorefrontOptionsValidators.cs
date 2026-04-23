namespace BlazorShop.Storefront.Configuration
{
    using BlazorShop.Application.Options;
    using BlazorShop.Storefront.Options;

    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;

    public sealed class StorefrontApiOptionsValidator : IValidateOptions<StorefrontApiOptions>
    {
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _hostEnvironment;

        public StorefrontApiOptionsValidator(IConfiguration configuration, IHostEnvironment hostEnvironment)
        {
            _configuration = configuration;
            _hostEnvironment = hostEnvironment;
        }

        public ValidateOptionsResult Validate(string? name, StorefrontApiOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.BaseUrl) && !IsAbsoluteHttpUrl(options.BaseUrl))
            {
                return ValidateOptionsResult.Fail("Api:BaseUrl must be an absolute http or https URL when configured.");
            }

            if (_hostEnvironment.IsDevelopment() || HasServiceDiscoveryEndpoint("apiservice"))
            {
                return ValidateOptionsResult.Success;
            }

            return string.IsNullOrWhiteSpace(options.BaseUrl)
                ? ValidateOptionsResult.Fail("Api:BaseUrl is required outside Development when Services:apiservice:* is not configured.")
                : ValidateOptionsResult.Success;
        }

        private bool HasServiceDiscoveryEndpoint(string serviceName)
        {
            return IsAbsoluteHttpUrl(_configuration[$"Services:{serviceName}:https:0"])
                || IsAbsoluteHttpUrl(_configuration[$"Services:{serviceName}:http:0"]);
        }

        private static bool IsAbsoluteHttpUrl(string? value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }

    public sealed class StorefrontClientAppOptionsValidator : IValidateOptions<ClientAppOptions>
    {
        private readonly IConfiguration _configuration;
        private readonly IHostEnvironment _hostEnvironment;

        public StorefrontClientAppOptionsValidator(IConfiguration configuration, IHostEnvironment hostEnvironment)
        {
            _configuration = configuration;
            _hostEnvironment = hostEnvironment;
        }

        public ValidateOptionsResult Validate(string? name, ClientAppOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.BaseUrl) && !IsAbsoluteHttpUrl(options.BaseUrl))
            {
                return ValidateOptionsResult.Fail("ClientApp:BaseUrl must be an absolute http or https URL when configured.");
            }

            if (_hostEnvironment.IsDevelopment() || HasServiceDiscoveryEndpoint("adminclient"))
            {
                return ValidateOptionsResult.Success;
            }

            return string.IsNullOrWhiteSpace(options.BaseUrl)
                ? ValidateOptionsResult.Fail("ClientApp:BaseUrl is required outside Development when Services:adminclient:* is not configured.")
                : ValidateOptionsResult.Success;
        }

        private bool HasServiceDiscoveryEndpoint(string serviceName)
        {
            return IsAbsoluteHttpUrl(_configuration[$"Services:{serviceName}:https:0"])
                || IsAbsoluteHttpUrl(_configuration[$"Services:{serviceName}:http:0"]);
        }

        private static bool IsAbsoluteHttpUrl(string? value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }

    public sealed class StorefrontPublicUrlOptionsValidator : IValidateOptions<StorefrontPublicUrlOptions>
    {
        private readonly IHostEnvironment _hostEnvironment;

        public StorefrontPublicUrlOptionsValidator(IHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        public ValidateOptionsResult Validate(string? name, StorefrontPublicUrlOptions options)
        {
            if (!string.IsNullOrWhiteSpace(options.BaseUrl) && !IsAbsoluteHttpUrl(options.BaseUrl))
            {
                return ValidateOptionsResult.Fail("PublicUrl:BaseUrl must be an absolute http or https URL when configured.");
            }

            if (_hostEnvironment.IsDevelopment())
            {
                return ValidateOptionsResult.Success;
            }

            return string.IsNullOrWhiteSpace(options.BaseUrl)
                ? ValidateOptionsResult.Fail("PublicUrl:BaseUrl is required outside Development so canonical and discovery URLs do not depend on request-host inference.")
                : ValidateOptionsResult.Success;
        }

        private static bool IsAbsoluteHttpUrl(string? value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }
    }
}