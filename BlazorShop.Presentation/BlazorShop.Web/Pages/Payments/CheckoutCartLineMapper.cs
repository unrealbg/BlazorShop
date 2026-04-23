namespace BlazorShop.Web.Pages.Payments
{
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;

    public static class CheckoutCartLineMapper
    {
        public static IReadOnlyList<CheckoutCartLine> Build(IEnumerable<ProcessCart> cartItems, IEnumerable<GetProduct> products)
        {
            var productMap = products
                .Where(product => product.Id != Guid.Empty)
                .GroupBy(product => product.Id)
                .ToDictionary(group => group.Key, group => group.First());

            return cartItems
                .Where(item => item.ProductId != Guid.Empty && item.Quantity > 0)
                .Select(item => BuildLine(item, productMap))
                .OrderBy(line => line.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(line => line.SizeValue, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static CheckoutCartLine BuildLine(ProcessCart item, IReadOnlyDictionary<Guid, GetProduct> productMap)
        {
            if (!productMap.TryGetValue(item.ProductId, out var product))
            {
                return new CheckoutCartLine(
                    item.ProductId,
                    item.VariantId,
                    "Unavailable item",
                    item.SizeValue,
                    null,
                    item.UnitPrice ?? 0m,
                    item.Quantity,
                    IsUnavailable: true);
            }

            return new CheckoutCartLine(
                item.ProductId,
                item.VariantId,
                string.IsNullOrWhiteSpace(product.Name) ? "Product" : product.Name,
                item.SizeValue,
                product.Image,
                item.UnitPrice ?? product.Price,
                item.Quantity,
                IsUnavailable: false);
        }
    }
}