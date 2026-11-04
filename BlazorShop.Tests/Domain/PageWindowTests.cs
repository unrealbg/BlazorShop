namespace BlazorShop.Tests.Domain
{
    using BlazorShop.Domain.Contracts;

    using Xunit;

    public sealed class PageWindowTests
    {
        [Theory]
        [InlineData(1, 24, 0L)]
        [InlineData(2, 24, 24L)]
        [InlineData(0, 24, 0L)]
        [InlineData(-5, 24, 0L)]
        [InlineData(3, 0, 2L)]
        [InlineData(int.MaxValue, 100, (int.MaxValue - 1L) * 100L)]
        public void GetOffset_UsesSixtyFourBitArithmetic(int pageNumber, int pageSize, long expected)
        {
            Assert.Equal(expected, PageWindow.GetOffset(pageNumber, pageSize));
        }

        [Fact]
        public void IsRepresentable_AcceptsLargestIntOffsetAndRejectsTheNextPage()
        {
            const int pageSize = 100;
            var lastSafePage = (int)((int.MaxValue / (long)pageSize) + 1);

            Assert.True(PageWindow.IsRepresentable(lastSafePage, pageSize));
            Assert.False(PageWindow.IsRepresentable(lastSafePage + 1, pageSize));
            Assert.False(PageWindow.IsRepresentable(int.MaxValue, pageSize));
        }

        [Fact]
        public void GetSkip_SaturatesInsteadOfOverflowing()
        {
            Assert.Equal(int.MaxValue, PageWindow.GetSkip(int.MaxValue, 100));
            Assert.Equal(0, PageWindow.GetSkip(int.MinValue, int.MinValue));
        }

        [Fact]
        public void ProductCatalogQuery_UsesNormalizedValuesForPageWindow()
        {
            var overflowing = new ProductCatalogQuery { PageNumber = int.MaxValue, PageSize = 100 };
            var clampedPageSize = new ProductCatalogQuery { PageNumber = 2, PageSize = int.MaxValue };

            Assert.False(overflowing.HasRepresentablePageWindow());
            Assert.Equal(int.MaxValue, overflowing.GetSkip());
            Assert.True(clampedPageSize.HasRepresentablePageWindow());
            Assert.Equal(100, clampedPageSize.GetSkip());
        }
    }
}
