namespace BlazorShop.Web.Shared.CookieStorage
{
    using System.Text.Json;

    using BlazorShop.Web.Shared.Models.Payment;

    public static class CartCookieParser
    {
        public const string InvalidItemsMessage = "Some saved cart items were invalid and were ignored. Review the remaining items before checkout.";
        public const string MalformedMessage = "We couldn't read the saved cart cookie. Add the items again to continue.";

        public static CartCookieParseResult Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new([], false, false);
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return new([], true, true);
                }

                var items = new Dictionary<(Guid ProductId, Guid? VariantId), ProcessCart>();
                var rejectedKeys = new HashSet<(Guid ProductId, Guid? VariantId)>();
                var invalid = false;
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object
                        || !TryGuid(Property(element, "ProductId", "productId"), out var productId))
                    {
                        invalid = true;
                        continue;
                    }

                    var variantElement = Property(element, "VariantId", "variantId");
                    Guid? variantId = null;
                    if (variantElement.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                    {
                        if (!TryGuid(variantElement, out var parsedVariant))
                        {
                            invalid = true;
                            continue;
                        }

                        variantId = parsedVariant;
                    }

                    var quantityElement = Property(element, "Quantity", "quantity");
                    if (quantityElement.ValueKind != JsonValueKind.Number
                        || !quantityElement.TryGetDouble(out var quantity)
                        || !double.IsFinite(quantity) || quantity != Math.Truncate(quantity)
                        || quantity <= 0 || quantity > int.MaxValue)
                    {
                        invalid = true;
                        continue;
                    }

                    var size = Property(element, "SizeValue", "sizeValue");
                    var price = Property(element, "UnitPrice", "unitPrice");
                    var sizeValue = size.ValueKind == JsonValueKind.String ? size.GetString() : null;
                    decimal? unitPrice = null;
                    if (size.ValueKind is not (JsonValueKind.String or JsonValueKind.Null or JsonValueKind.Undefined))
                    {
                        invalid = true;
                    }

                    if (price.ValueKind == JsonValueKind.Number && price.TryGetDecimal(out var parsedPrice))
                    {
                        unitPrice = parsedPrice;
                    }
                    else if (price.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined))
                    {
                        invalid = true;
                    }

                    var key = (productId, variantId);
                    if (rejectedKeys.Contains(key))
                    {
                        continue;
                    }

                    if (items.TryGetValue(key, out var existing))
                    {
                        try
                        {
                            existing.Quantity = checked(existing.Quantity + (int)quantity);
                        }
                        catch (OverflowException)
                        {
                            items.Remove(key);
                            rejectedKeys.Add(key);
                            invalid = true;
                        }
                    }
                    else
                    {
                        items.Add(key, new ProcessCart
                        {
                            ProductId = productId,
                            VariantId = variantId,
                            Quantity = (int)quantity,
                            SizeValue = sizeValue,
                            UnitPrice = unitPrice,
                        });
                    }
                }

                return new(items.Values.ToArray(), invalid, false);
            }
            catch (JsonException)
            {
                return new([], true, true);
            }
        }

        private static JsonElement Property(JsonElement element, string name, string alias)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null)
            {
                return value;
            }

            return element.TryGetProperty(alias, out value) ? value : default;
        }

        private static bool TryGuid(JsonElement element, out Guid id)
        {
            id = Guid.Empty;
            var text = element.ValueKind == JsonValueKind.String ? element.GetString() : null;
            return text is { Length: 36 }
                && Guid.TryParseExact(text, "D", out id) && id != Guid.Empty;
        }
    }

    public sealed record CartCookieParseResult(IReadOnlyList<ProcessCart> Items, bool HasInvalidItems, bool IsMalformed);
}
