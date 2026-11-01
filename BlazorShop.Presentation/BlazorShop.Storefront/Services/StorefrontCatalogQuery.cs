namespace BlazorShop.Storefront.Services
{
    using System.Globalization;

    using BlazorShop.Web.Shared.Models.Product;

    public sealed record StorefrontCatalogQuery
    {
        public const int PageSize = 24;
        public const int MaxSearchLength = 200;
        // Keep the existing API's integer Skip/Take arithmetic within range.
        public const int MaxPageNumber = int.MaxValue / PageSize;

        public static IReadOnlyList<(string Value, string Label, ProductCatalogSortBy Sort)> SortOptions { get; } =
        [
            ("newest", "Newest first", ProductCatalogSortBy.Newest),
            ("oldest", "Oldest first", ProductCatalogSortBy.Oldest),
            ("price-low-to-high", "Price: low to high", ProductCatalogSortBy.PriceLowToHigh),
            ("price-high-to-low", "Price: high to low", ProductCatalogSortBy.PriceHighToLow),
            ("name-ascending", "Name: A to Z", ProductCatalogSortBy.NameAscending),
            ("name-descending", "Name: Z to A", ProductCatalogSortBy.NameDescending),
        ];

        public string? SearchTerm { get; private init; }

        public Guid? CategoryId { get; private init; }

        public string Sort { get; private init; } = "newest";

        public ProductCatalogSortBy SortBy { get; private init; } = ProductCatalogSortBy.Newest;

        public int PageNumber { get; private init; } = 1;

        public bool IsInvalid { get; private init; }

        public bool DefaultsApplied { get; private init; }

        public static StorefrontCatalogQuery Parse(string? search, string? category, string? sort, string? page)
        {
            var term = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var categoryValid = string.IsNullOrWhiteSpace(category)
                || (Guid.TryParse(category, out var parsedCategory) && parsedCategory != Guid.Empty);
            var selectedSort = SortOptions.FirstOrDefault(option => string.Equals(option.Value, sort, StringComparison.OrdinalIgnoreCase));
            var pageValid = int.TryParse(page, NumberStyles.None, CultureInfo.InvariantCulture, out var pageNumber) && pageNumber > 0;
            // A positive numeric overflow is an unavailable page, not page one.
            var excessivePage = !string.IsNullOrWhiteSpace(page)
                && page.All(char.IsAsciiDigit)
                && (!pageValid || pageNumber > MaxPageNumber)
                && page.TrimStart('0').Length > 0;
            return new StorefrontCatalogQuery
            {
                SearchTerm = term,
                CategoryId = categoryValid && Guid.TryParse(category, out var id) ? id : null,
                Sort = selectedSort.Value ?? "newest",
                SortBy = selectedSort.Value is null ? ProductCatalogSortBy.Newest : selectedSort.Sort,
                PageNumber = pageValid && !excessivePage ? pageNumber : 1,
                IsInvalid = !categoryValid || term?.Length > MaxSearchLength || excessivePage,
                DefaultsApplied = (!string.IsNullOrEmpty(sort) && selectedSort.Value is null)
                    || (!string.IsNullOrEmpty(page) && !pageValid && !excessivePage),
            };
        }

        public ProductCatalogQuery ToApiQuery() => new()
        {
            SearchTerm = SearchTerm,
            CategoryId = CategoryId,
            SortBy = SortBy,
            PageNumber = PageNumber,
            PageSize = PageSize,
        };

        public string Url(int? pageNumber = null)
        {
            var parameters = new List<string>();
            if (SearchTerm is not null)
            {
                parameters.Add($"q={Uri.EscapeDataString(SearchTerm)}");
            }

            if (CategoryId.HasValue)
            {
                parameters.Add($"category={CategoryId.Value:D}");
            }

            if (Sort != "newest")
            {
                parameters.Add($"sort={Sort}");
            }

            var currentPage = pageNumber ?? PageNumber;
            if (currentPage > 1)
            {
                parameters.Add($"page={currentPage.ToString(CultureInfo.InvariantCulture)}");
            }

            return parameters.Count == 0 ? StorefrontRoutes.Catalog : $"{StorefrontRoutes.Catalog}?{string.Join("&", parameters)}";
        }

        public IEnumerable<int> PaginationPages(int totalPages) => new[] { 1, totalPages }
            .Concat(Enumerable.Range(Math.Max(1, PageNumber - 2), Math.Max(0, Math.Min(totalPages, PageNumber + 2) - Math.Max(1, PageNumber - 2) + 1)))
            .Where(page => page > 0 && page <= totalPages)
            .Distinct()
            .Order();
    }
}
