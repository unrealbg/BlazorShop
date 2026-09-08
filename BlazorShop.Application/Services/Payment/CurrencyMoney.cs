namespace BlazorShop.Application.Services.Payment
{
    public static class CurrencyMoney
    {
        private static readonly IReadOnlyDictionary<string, int> SupportedCurrencies =
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["EUR"] = 2,
                ["GBP"] = 2,
                ["USD"] = 2,
            };

        public static string NormalizeCurrency(string? currency) =>
            currency?.Trim().ToUpperInvariant() ?? string.Empty;

        public static bool IsSupportedCurrency(string? currency) =>
            SupportedCurrencies.ContainsKey(NormalizeCurrency(currency));

        public static int GetMinorUnitPrecision(string currency)
        {
            var normalized = NormalizeCurrency(currency);
            return SupportedCurrencies.TryGetValue(normalized, out var precision)
                ? precision
                : throw new ArgumentOutOfRangeException(
                    nameof(currency),
                    currency,
                    "The configured commerce currency is not supported.");
        }

        public static decimal NormalizeAmount(decimal amount, string currency)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), "Money cannot be negative.");
            }

            return decimal.Round(
                amount,
                GetMinorUnitPrecision(currency),
                MidpointRounding.AwayFromZero);
        }

        public static long ToMinorUnits(decimal amount, string currency)
        {
            var precision = GetMinorUnitPrecision(currency);
            var normalized = NormalizeAmount(amount, currency);
            var multiplier = DecimalMultiplier(precision);
            return checked(decimal.ToInt64(normalized * multiplier));
        }

        public static decimal FromMinorUnits(long amountMinor, string currency)
        {
            if (amountMinor < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amountMinor), "Money cannot be negative.");
            }

            return amountMinor / DecimalMultiplier(GetMinorUnitPrecision(currency));
        }

        private static decimal DecimalMultiplier(int precision)
        {
            var multiplier = 1m;
            for (var index = 0; index < precision; index++)
            {
                multiplier *= 10m;
            }

            return multiplier;
        }
    }
}
