namespace BlazorShop.Tests.Presentation.Storefront
{
    using System.Net;

    internal sealed class StorefrontSeoSmokeClient : IDisposable
    {
        private static readonly DecompressionMethods SupportedDecompressionMethods = DecompressionMethods.Brotli | DecompressionMethods.Deflate | DecompressionMethods.GZip;

        private readonly HttpClient _client;

        public StorefrontSeoSmokeClient(StorefrontSeoSmokeSettings settings)
        {
            Settings = settings;

            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = SupportedDecompressionMethods,
            };

            if (settings.AllowInvalidCertificate)
            {
                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            _client = new HttpClient(handler)
            {
                BaseAddress = settings.BaseUri,
                Timeout = TimeSpan.FromSeconds(20),
            };

            _client.DefaultRequestHeaders.UserAgent.ParseAdd("BlazorShop-Seo-Smoke/1.0");
        }

        public StorefrontSeoSmokeSettings Settings { get; }

        public Task<HttpResponseMessage> GetAsync(string routePathOrAbsoluteUrl, CancellationToken cancellationToken = default)
        {
            return _client.GetAsync(Settings.ToRequestTarget(routePathOrAbsoluteUrl), cancellationToken);
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}