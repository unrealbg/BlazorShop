namespace BlazorShop.Tests.Application.Services
{
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services;
    using BlazorShop.Application.Services.Contracts;

    using Xunit;

    public class SeoMetadataBuilderTests
    {
        private readonly ISeoMetadataBuilder _builder = new SeoMetadataBuilder();

        [Fact]
        public void Build_WhenEntitySeoIsPartial_ComposesMetadataFromEntityAndGlobalDefaults()
        {
            var request = new SeoMetadataBuildRequest
            {
                PageTitle = "Running Shoes",
                RelativePath = "/products/running-shoes",
                PageSeo = new SeoFieldsDto
                {
                    MetaTitle = "Best Running Shoes",
                    OgImage = "/images/og/shoes.png",
                    RobotsFollow = false,
                },
                Settings = new SeoSettingsDto
                {
                    SiteName = "BlazorShop",
                    DefaultTitleSuffix = "| BlazorShop",
                    DefaultMetaDescription = "Shop the latest catalog.",
                    DefaultOgImage = "https://cdn.example.com/default-og.png",
                    BaseCanonicalUrl = "https://shop.example.com",
                },
            };

            var result = this._builder.Build(request);

            Assert.Equal("Best Running Shoes | BlazorShop", result.Title);
            Assert.Equal("Shop the latest catalog.", result.MetaDescription);
            Assert.Equal("https://shop.example.com/products/running-shoes", result.CanonicalUrl);
            Assert.Equal("Best Running Shoes | BlazorShop", result.OgTitle);
            Assert.Equal("Shop the latest catalog.", result.OgDescription);
            Assert.Equal("https://shop.example.com/images/og/shoes.png", result.OgImage);
            Assert.Equal("BlazorShop", result.SiteName);
            Assert.True(result.RobotsIndex);
            Assert.False(result.RobotsFollow);
        }
    }
}