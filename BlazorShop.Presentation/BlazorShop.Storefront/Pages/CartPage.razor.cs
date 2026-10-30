namespace BlazorShop.Storefront.Pages
{
    using System.Globalization;

    using BlazorShop.Storefront.Services;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.CookieStorage;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;

    using Microsoft.AspNetCore.Components;

    public partial class CartPage
    {
        private readonly List<CartAlert> _alerts = [];
        private IReadOnlyList<CartLine> _lines = [];
        private decimal? _grandTotal = 0m;

        [CascadingParameter]
        private HttpContext? HttpContext { get; set; }

        private IReadOnlyList<CartAlert> Alerts => _alerts;

        private IReadOnlyList<CartLine> Lines => _lines;

        private long ItemCount => _lines.Sum(line => (long)line.Quantity);

        private string GrandTotalDisplay => _grandTotal.HasValue ? $"EUR {_grandTotal.Value.ToString("0.00", CultureInfo.InvariantCulture)}" : "Unavailable";

        private string CheckoutUrl => StorefrontRoutes.Checkout;

        protected override async Task OnParametersSetAsync()
        {
            _alerts.Clear();
            StorefrontResponseHeaders.ApplyPrivatePage(HttpContext);

            var cartItems = ReadCartItems(HttpContext?.Request.Cookies[Constant.Cart.Name]);
            var productsById = await LoadProductsAsync(cartItems);
            _lines = BuildLines(cartItems, productsById);
            try
            {
                _grandTotal = _lines.Sum(line => line.LineTotal);
            }
            catch (OverflowException)
            {
                _grandTotal = null;
                _alerts.Add(new CartAlert("error", "Some cart totals cannot be displayed safely. Review quantities before checkout."));
            }
        }

        private List<ProcessCart> ReadCartItems(string? rawCart)
        {
            var result = CartCookieParser.Parse(rawCart);
            if (result.HasInvalidItems)
            {
                _alerts.Add(new CartAlert(
                    result.IsMalformed ? "error" : "warning",
                    result.IsMalformed ? CartCookieParser.MalformedMessage : CartCookieParser.InvalidItemsMessage));
            }

            return result.Items.ToList();
        }

        private async Task<Dictionary<Guid, GetProduct>> LoadProductsAsync(IEnumerable<ProcessCart> cartItems)
        {
            var productIds = cartItems
                .Select(item => item.ProductId)
                .Where(productId => productId != Guid.Empty)
                .Distinct()
                .ToArray();

            if (productIds.Length == 0)
            {
                return [];
            }

            var results = await Task.WhenAll(productIds.Select(id => ApiClient.GetProductByIdAsync(id)));
            var productsById = new Dictionary<Guid, GetProduct>();

            for (var index = 0; index < productIds.Length; index++)
            {
                var result = results[index];
                if (result.IsSuccess && result.Value is not null)
                {
                    productsById[productIds[index]] = result.Value;
                }
            }

            return productsById;
        }

        private IReadOnlyList<CartLine> BuildLines(IEnumerable<ProcessCart> cartItems, IReadOnlyDictionary<Guid, GetProduct> productsById)
        {
            var lines = new List<CartLine>();
            var unavailableItems = 0;

            foreach (var cartItem in cartItems)
            {
                var quantity = Math.Max(1, cartItem.Quantity);
                if (productsById.TryGetValue(cartItem.ProductId, out var product))
                {
                    var variant = cartItem.VariantId.HasValue
                        ? product.Variants.FirstOrDefault(candidate => candidate.Id == cartItem.VariantId.Value)
                        : null;
                    var isUnavailable = cartItem.VariantId.HasValue
                        ? variant is null || variant.Stock <= 0
                        : product.Quantity <= 0;

                    if (isUnavailable)
                    {
                        unavailableItems++;
                    }

                    lines.Add(new CartLine(
                        ProductId: cartItem.ProductId,
                        VariantId: cartItem.VariantId,
                        DisplayName: string.IsNullOrWhiteSpace(product.Name) ? "Product" : product.Name,
                        ProductUrl: string.IsNullOrWhiteSpace(product.Slug) ? null : StorefrontRoutes.Product(product.Slug),
                        ImageUrl: product.Image,
                        Quantity: quantity,
                        UnitPrice: variant?.Price ?? product.Price,
                        SizeValue: variant?.SizeValue,
                        Sku: variant?.Sku,
                        Color: variant?.Color,
                        IsUnavailable: isUnavailable));
                    continue;
                }

                unavailableItems++;
                lines.Add(new CartLine(
                    ProductId: cartItem.ProductId,
                    VariantId: cartItem.VariantId,
                    DisplayName: "Unavailable item",
                    ProductUrl: null,
                    ImageUrl: null,
                    Quantity: quantity,
                    UnitPrice: 0m,
                    SizeValue: null,
                    Sku: null,
                    Color: null,
                    IsUnavailable: true));
            }

            if (unavailableItems > 0)
            {
                _alerts.Add(new CartAlert(
                    "warning",
                    unavailableItems == 1
                        ? "One cart item could not be refreshed from the catalog and is shown as unavailable."
                        : $"{unavailableItems} cart items could not be refreshed from the catalog and are shown as unavailable."));
            }

            return lines;
        }

        private sealed record CartAlert(string Level, string Message);

        private sealed record CartLine(
            Guid ProductId,
            Guid? VariantId,
            string DisplayName,
            string? ProductUrl,
            string? ImageUrl,
            int Quantity,
            decimal UnitPrice,
            string? SizeValue,
            string? Sku,
            string? Color,
            bool IsUnavailable)
        {
            public decimal LineTotal => UnitPrice * Quantity;

            public string UnitPriceDisplay => UnitPrice.ToString("0.00", CultureInfo.InvariantCulture);

            public string LineTotalDisplay
            {
                get
                {
                    try
                    {
                        return $"EUR {LineTotal.ToString("0.00", CultureInfo.InvariantCulture)}";
                    }
                    catch (OverflowException)
                    {
                        return "Unavailable";
                    }
                }
            }

            public string? VariantLabel
            {
                get
                {
                    var details = new[]
                        {
                            string.IsNullOrWhiteSpace(Sku) ? null : $"SKU {Sku}",
                            string.IsNullOrWhiteSpace(SizeValue) ? null : $"Size {SizeValue}",
                            string.IsNullOrWhiteSpace(Color) ? null : Color,
                        }
                        .Where(value => value is not null)
                        .ToArray();

                    return details.Length == 0 ? null : string.Join(" · ", details);
                }
            }
        }
    }
}
