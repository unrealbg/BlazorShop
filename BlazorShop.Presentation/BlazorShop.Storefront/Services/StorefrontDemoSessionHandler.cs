namespace BlazorShop.Storefront.Services
{
    using BlazorShop.Application.Options;

    using Microsoft.Extensions.Options;

    public sealed class StorefrontDemoSessionHandler : DelegatingHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly DemoOptions _options;

        public StorefrontDemoSessionHandler(
            IHttpContextAccessor httpContextAccessor,
            IOptions<DemoOptions> options)
        {
            _httpContextAccessor = httpContextAccessor;
            _options = options.Value;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is not null
                && httpContext.Request.Cookies.TryGetValue(_options.CookieName, out var token)
                && !string.IsNullOrWhiteSpace(token))
            {
                request.Headers.TryAddWithoutValidation(_options.HeaderName, token);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
