namespace BlazorShop.Web.Shared.Services
{
    using System.Globalization;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;

    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Web.Shared.BrowserStorage.Contracts;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Services.Contracts;

    public sealed class CheckoutAttemptStore : ICheckoutAttemptStore
    {
        private const string StorageKey = "blazorshop.checkout.pending";
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
        private readonly IBrowserSessionStorageService _sessionStorage;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public CheckoutAttemptStore(IBrowserSessionStorageService sessionStorage)
        {
            _sessionStorage = sessionStorage;
        }

        public async Task<CheckoutAttempt> GetOrCreateAsync(Checkout checkout)
        {
            var signature = CreateIntentSignature(checkout);
            await _gate.WaitAsync();
            try
            {
                var stored = await ReadAsync();
                if (stored is not null
                    && string.Equals(stored.IntentSignature, signature, StringComparison.Ordinal))
                {
                    return stored;
                }

                var attempt = new CheckoutAttempt(Guid.NewGuid(), signature);
                await _sessionStorage.SetAsync(StorageKey, JsonSerializer.Serialize(attempt, SerializerOptions));
                return attempt;
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task ClearAsync(Guid completedKey)
        {
            await _gate.WaitAsync();
            try
            {
                var stored = await ReadAsync();
                if (stored?.IdempotencyKey == completedKey)
                {
                    await _sessionStorage.RemoveAsync(StorageKey);
                }
            }
            finally
            {
                _gate.Release();
            }
        }

        public static string CreateIntentSignature(Checkout checkout)
        {
            ArgumentNullException.ThrowIfNull(checkout);

            var quantities = new Dictionary<(Guid ProductId, Guid? VariantId), int>();
            foreach (var line in checkout.Carts ?? [])
            {
                var key = (line.ProductId, line.VariantId);
                quantities.TryGetValue(key, out var quantity);
                quantities[key] = checked(quantity + line.Quantity);
            }

            var canonical = new StringBuilder("checkout-v1\n")
                .Append("payment:")
                .Append(checkout.PaymentMethodId.ToString("N"))
                .Append('\n');
            foreach (var line in quantities
                .Select(item => new CartLineRequest(item.Key.ProductId, item.Key.VariantId, item.Value))
                .OrderBy(line => line.ProductId)
                .ThenBy(line => line.VariantId))
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

            return Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
        }

        private async Task<CheckoutAttempt?> ReadAsync()
        {
            var json = await _sessionStorage.GetAsync(StorageKey);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<CheckoutAttempt>(json, SerializerOptions);
            }
            catch (JsonException)
            {
                await _sessionStorage.RemoveAsync(StorageKey);
                return null;
            }
        }
    }
}
