namespace BlazorShop.Web.Components.Search
{
    using BlazorShop.Web.Shared.Models.Product;

    public static class SearchCatalogUrl
    {
        public static string Build(
            string searchTerm,
            ProductCatalogSortBy sortBy,
            Guid? categoryId = null)
        {
            var normalizedSearchTerm = searchTerm?.Trim() ?? string.Empty;
            var parameters = new List<string>
            {
                $"sort={Uri.EscapeDataString(sortBy.ToString())}",
            };

            if (categoryId.HasValue && categoryId.Value != Guid.Empty)
            {
                parameters.Add($"category={categoryId.Value:D}");
            }

            return $"/search-result/{Uri.EscapeDataString(normalizedSearchTerm)}?{string.Join("&", parameters)}";
        }
    }
}
