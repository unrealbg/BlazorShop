namespace BlazorShop.Application.Services.Payment
{
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;

    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Domain.Contracts.Payment;

    public static class CheckoutIntentCanonicalizer
    {
        public static CheckoutIntentCanonicalization Canonicalize(Checkout checkout)
        {
            ArgumentNullException.ThrowIfNull(checkout);

            if (checkout.PaymentMethodId == Guid.Empty)
            {
                return CheckoutIntentCanonicalization.Failure("A valid payment method is required.");
            }

            var quantities = new Dictionary<(Guid ProductId, Guid? VariantId), int>();
            foreach (var line in checkout.Carts ?? [])
            {
                if (line.ProductId == Guid.Empty
                    || line.VariantId == Guid.Empty
                    || line.Quantity <= 0)
                {
                    return CheckoutIntentCanonicalization.Failure(
                        "Every cart item must have a valid product, variant, and quantity.");
                }

                var key = (line.ProductId, line.VariantId);
                quantities.TryGetValue(key, out var current);
                try
                {
                    quantities[key] = checked(current + line.Quantity);
                }
                catch (OverflowException)
                {
                    return CheckoutIntentCanonicalization.Failure(
                        "The requested inventory quantity is too large.");
                }
            }

            if (quantities.Count == 0)
            {
                return CheckoutIntentCanonicalization.Failure("Your cart is empty.");
            }

            var lines = quantities
                .Select(item => new CartLineRequest(item.Key.ProductId, item.Key.VariantId, item.Value))
                .OrderBy(line => line.ProductId)
                .ThenBy(line => line.VariantId)
                .ToArray();
            var canonical = new StringBuilder("checkout-v1\n")
                .Append("payment:")
                .Append(checkout.PaymentMethodId.ToString("N"))
                .Append('\n');

            foreach (var line in lines)
            {
                canonical
                    .Append("product:")
                    .Append(line.ProductId.ToString("N"))
                    .Append("|variant:")
                    .Append(line.VariantId?.ToString("N") ?? "-")
                    .Append("|quantity:")
                    .Append(line.Quantity.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
            }

            var fingerprint = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
            return CheckoutIntentCanonicalization.Success(fingerprint, lines);
        }
    }

    public sealed record CheckoutIntentCanonicalization(
        bool IsValid,
        string? Fingerprint,
        IReadOnlyList<CartLineRequest> Lines,
        string? ErrorMessage)
    {
        public static CheckoutIntentCanonicalization Success(
            string fingerprint,
            IReadOnlyList<CartLineRequest> lines) => new(true, fingerprint, lines, null);

        public static CheckoutIntentCanonicalization Failure(string errorMessage) =>
            new(false, null, [], errorMessage);
    }
}
