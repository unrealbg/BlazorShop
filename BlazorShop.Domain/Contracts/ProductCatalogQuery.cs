namespace BlazorShop.Domain.Contracts
{
    public sealed class ProductCatalogQuery
    {
        private const int DefaultPageNumber = 1;
        private const int DefaultPageSize = 24;
        private const int MaxPageSize = 100;

        public int PageNumber { get; init; } = DefaultPageNumber;

        public int PageSize { get; init; } = DefaultPageSize;

        public Guid? CategoryId { get; init; }

        public string? SearchTerm { get; init; }

        public ProductCatalogSortBy SortBy { get; init; } = ProductCatalogSortBy.Newest;

        public DateTime? CreatedAfterUtc { get; init; }

        public int GetNormalizedPageNumber() => this.PageNumber < 1 ? DefaultPageNumber : this.PageNumber;

        public int GetNormalizedPageSize() => Math.Clamp(this.PageSize, 1, MaxPageSize);

        public string? GetNormalizedSearchTerm() => string.IsNullOrWhiteSpace(this.SearchTerm)
            ? null
            : this.SearchTerm.Trim();
    }
}