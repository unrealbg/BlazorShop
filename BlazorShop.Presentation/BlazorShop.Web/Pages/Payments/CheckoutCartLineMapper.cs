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
                    null,
                    null,
                    null,
                    null,
                    0m,
                    item.Quantity,
                    IsUnavailable: true);
            }

            var variant = item.VariantId.HasValue
                ? product.Variants.FirstOrDefault(candidate => candidate.Id == item.VariantId.Value)
                : null;
            var isUnavailable = item.VariantId.HasValue
                ? variant is null || variant.Stock <= 0
                : product.Quantity <= 0;

            return new CheckoutCartLine(
                item.ProductId,
                item.VariantId,
                string.IsNullOrWhiteSpace(product.Name) ? "Product" : product.Name,
                variant?.SizeValue,
                variant?.Sku,
                variant?.Color,
                product.Image,
                variant?.Price ?? product.Price,
                item.Quantity,
                IsUnavailable: isUnavailable);
        }
    }
}
