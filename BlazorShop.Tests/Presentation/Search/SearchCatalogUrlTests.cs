namespace BlazorShop.Tests.Presentation.Search
{
    using BlazorShop.Web.Components.Search;
    using BlazorShop.Web.Shared.Models.Product;

    using Xunit;

    public sealed class SearchCatalogUrlTests
    {
        [Fact]
        public void Build_PersistsSearchSortAndCategory()
        {
            var categoryId = Guid.NewGuid();

            var url = SearchCatalogUrl.Build(
                " running shoes ",
                ProductCatalogSortBy.PriceLowToHigh,
                categoryId);

            Assert.Equal(
                $"/search-result/running%20shoes?sort=PriceLowToHigh&category={categoryId:D}",
                url);
        }

        [Fact]
        public void Build_WithoutCategory_LeavesNoEmptyCategoryParameter()
        {
            var url = SearchCatalogUrl.Build("camera", ProductCatalogSortBy.Newest);

            Assert.Equal("/search-result/camera?sort=Newest", url);
        }
    }
}
