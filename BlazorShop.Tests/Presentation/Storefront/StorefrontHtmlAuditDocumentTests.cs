namespace BlazorShop.Tests.Presentation.Storefront
{
    using Xunit;

    public class StorefrontHtmlAuditDocumentTests
    {
        [Fact]
        public void Create_TracksBrokenAssetReferencesWithoutTreatingCanonicalsAsAssets()
        {
            const string html = """
                <html>
                <head>
                    <link rel="canonical" href="https://shop.example.com/product/metro-runner" />
                    <link rel="stylesheet" href="/css/site.css" />
                    <script src="undefined"></script>
                </head>
                <body>
                    <img src="/uploads/metro-runner.png" />
                    <a href="/product/metro-runner">Metro Runner</a>
                </body>
                </html>
                """;

            var document = StorefrontHtmlAuditDocument.Create(html);

            Assert.Contains("/css/site.css", document.AssetUrls);
            Assert.Contains("/uploads/metro-runner.png", document.AssetUrls);
            Assert.Contains("undefined", document.BrokenAssetUrls);
            Assert.DoesNotContain("https://shop.example.com/product/metro-runner", document.AssetUrls);
        }
    }
}