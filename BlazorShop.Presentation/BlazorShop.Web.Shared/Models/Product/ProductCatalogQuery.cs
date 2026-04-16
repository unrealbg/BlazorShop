namespace BlazorShop.Web.Shared.Models.Product
{
    public sealed class ProductCatalogQuery
    {
        public int PageNumber { get; set; } = 1;

        public int PageSize { get; set; } = 24;

        public Guid? CategoryId { get; set; }

        public string? SearchTerm { get; set; }

        public ProductCatalogSortBy SortBy { get; set; } = ProductCatalogSortBy.Newest;

        public DateTime? CreatedAfterUtc { get; set; }
    }
}