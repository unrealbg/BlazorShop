namespace BlazorShop.Tests.Application.Services
{
    using BlazorShop.Application.Services;
    using BlazorShop.Application.Services.Contracts;

    using Xunit;

    public class SlugServiceTests
    {
        private readonly ISlugService _slugService = new SlugService();

        [Fact]
        public void NormalizeSlug_WhenTextContainsSpacesPunctuationAndDiacritics_ReturnsUrlSafeSlug()
        {
            var result = this._slugService.NormalizeSlug("  Crème Brûlée & Friends!  ");

            Assert.Equal("creme-brulee-friends", result);
        }

        [Fact]
        public void GenerateSlug_WhenTextCannotBeNormalized_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => this._slugService.GenerateSlug("!!!"));
        }

        [Theory]
        [InlineData("summer-sale-2026", true)]
        [InlineData("Summer Sale 2026", false)]
        [InlineData("summer_sale_2026", false)]
        public void IsSlugSafe_ReturnsExpectedResult(string slug, bool expected)
        {
            var result = this._slugService.IsSlugSafe(slug);

            Assert.Equal(expected, result);
        }
    }
}