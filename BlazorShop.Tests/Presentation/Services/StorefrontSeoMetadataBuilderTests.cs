namespace BlazorShop.Tests.Presentation.Services
{
    using BlazorShop.Web.Services;
    using BlazorShop.Web.Services.Contracts;
    using BlazorShop.Web.Shared.Models.Seo;

    using Xunit;

    public class StorefrontSeoMetadataBuilderTests
    {
        private readonly IStorefrontSeoMetadataBuilder _builder = new StorefrontSeoMetadataBuilder();

        [Fact]
        public void Build_WhenCategorySeoIsPartial_ComposesMetadataFromEntityAndGlobalDefaults()
        {
            var request = new StorefrontSeoMetadataBuildRequest
            {
                PageTitle = "Running Shoes Products",
                RelativePath = "/main/products/category/123",
                FallbackMetaDescription = "Browse running shoes.",
                PageSeo = new StorefrontSeoPageData
                {
                    MetaTitle = "Best Running Shoes",
                    OgImage = "/images/og/shoes.png",
                    RobotsFollow = false,
                },
                Settings = new GetSeoSettings
                {
                    SiteName = "BlazorShop",
                    DefaultTitleSuffix = "| BlazorShop",
                    DefaultMetaDescription = "Shop the latest catalog.",
                    DefaultOgImage = "https://cdn.example.com/default-og.png",
                    BaseCanonicalUrl = "https://shop.example.com",
                },
            };

            var result = _builder.Build(request);

            Assert.Equal("Best Running Shoes | BlazorShop", result.Title);
            Assert.Equal("Browse running shoes.", result.MetaDescription);
            Assert.Equal("https://shop.example.com/main/products/category/123", result.CanonicalUrl);
            Assert.Equal("Best Running Shoes | BlazorShop", result.OgTitle);
            Assert.Equal("Browse running shoes.", result.OgDescription);
            Assert.Equal("https://shop.example.com/images/og/shoes.png", result.OgImage);
            Assert.Equal("BlazorShop", result.SiteName);
            Assert.True(result.RobotsIndex);
            Assert.False(result.RobotsFollow);
        }

        [Fact]
        public void Build_WhenSeoDataIsMissing_FallsBackToPageValuesWithoutThrowing()
        {
            var request = new StorefrontSeoMetadataBuildRequest
            {
                PageTitle = "Customer Service",
                RelativePath = "/customer-service",
                FallbackMetaDescription = "Contact support.",
            };

            var result = _builder.Build(request);

            Assert.Equal("Customer Service", result.Title);
            Assert.Equal("Contact support.", result.MetaDescription);
            Assert.Equal("/customer-service", result.CanonicalUrl);
            Assert.Equal("Customer Service", result.OgTitle);
            Assert.Equal("Contact support.", result.OgDescription);
            Assert.Null(result.OgImage);
            Assert.True(result.RobotsIndex);
            Assert.True(result.RobotsFollow);
        }
    }
}