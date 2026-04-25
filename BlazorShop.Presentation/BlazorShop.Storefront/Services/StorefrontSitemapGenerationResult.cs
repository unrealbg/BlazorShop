namespace BlazorShop.Storefront.Services
{
    public sealed class StorefrontSitemapGenerationResult
    {
        private StorefrontSitemapGenerationResult(string? content, bool isServiceUnavailable)
        {
            Content = content;
            IsServiceUnavailable = isServiceUnavailable;
        }

        public string? Content { get; }

        public bool IsServiceUnavailable { get; }

        public static StorefrontSitemapGenerationResult Success(string content)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(content);
            return new StorefrontSitemapGenerationResult(content, isServiceUnavailable: false);
        }

        public static StorefrontSitemapGenerationResult ServiceUnavailable()
        {
            return new StorefrontSitemapGenerationResult(content: null, isServiceUnavailable: true);
        }
    }
}