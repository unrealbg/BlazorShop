namespace BlazorShop.API.Options
{
    public sealed class ApiRuntimeOptions
    {
        public const string SectionName = "Runtime";

        public ApiCorsOptions Cors { get; set; } = new();

        public ApiForwardedHeadersOptions ForwardedHeaders { get; set; } = new();

        public ApiHealthEndpointOptions Health { get; set; } = new();

        public PublicApiRateLimitingOptions RateLimiting { get; set; } = new();
    }

    public sealed class ApiCorsOptions
    {
        public string[] AllowedOrigins { get; set; } = [];
    }

    public sealed class ApiForwardedHeadersOptions
    {
        public bool Enabled { get; set; }

        public string[] KnownProxies { get; set; } = [];

        public string[] KnownNetworks { get; set; } = [];

        public int? ForwardLimit { get; set; } = 1;
    }

    public sealed class ApiHealthEndpointOptions
    {
        public bool ExposeInProduction { get; set; } = true;

        public string ReadyPath { get; set; } = "/health";

        public string LivePath { get; set; } = "/alive";
    }

    public sealed class PublicApiRateLimitingOptions
    {
        public bool Enabled { get; set; } = true;

        public int PermitLimit { get; set; } = 60;

        public int WindowSeconds { get; set; } = 60;

        public int QueueLimit { get; set; }
    }
}