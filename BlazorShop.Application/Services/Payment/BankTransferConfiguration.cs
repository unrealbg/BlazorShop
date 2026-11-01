namespace BlazorShop.Application.Services.Payment
{
    using BlazorShop.Application.DTOs.Payment;

    // Optional payment method: invalid settings disable fresh bank checkouts, not application startup.
    internal static class BankTransferConfiguration
    {
        public static BankTransferSettings? ValidatedSnapshot(BankTransferSettings settings)
        {
            var iban = settings.Iban?.Trim().Replace(" ", string.Empty).ToUpperInvariant();
            if (!IsIbanValid(iban) || !IsRequiredTextValid(settings.Beneficiary) || !IsRequiredTextValid(settings.BankName))
            {
                return null;
            }

            return new BankTransferSettings
            {
                Iban = iban!,
                Beneficiary = settings.Beneficiary.Trim(),
                BankName = settings.BankName.Trim(),
                AdditionalInfo = settings.AdditionalInfo,
            };
        }

        private static bool IsIbanValid(string? iban)
        {
            // Generic ISO 13616 structure and MOD-97 checksum. This does not establish
            // country-specific BBAN rules, bank existence, routability or merchant ownership.
            if (iban is null || iban.Length is < 15 or > 34
                || iban[0] is < 'A' or > 'Z' || iban[1] is < 'A' or > 'Z'
                || iban[2] is < '0' or > '9' || iban[3] is < '0' or > '9'
                || !iban[4..].All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9')
                || iban[4..].All(character => character == '0'))
            {
                return false;
            }

            var checkDigits = (iban[2] - '0') * 10 + iban[3] - '0';
            if (checkDigits is < 2 or > 98)
            {
                return false;
            }

            var remainder = 0;
            foreach (var character in iban[4..] + iban[..4])
            {
                var number = character is >= 'A' and <= 'Z' ? character - 'A' + 10 : character - '0';
                remainder = (remainder * (number >= 10 ? 100 : 10) + number) % 97;
            }

            return remainder == 1;
        }

        private static bool IsRequiredTextValid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            // Reject common template/sentinel values, including punctuation-delimited forms.
            // No finite list can prove these operator-supplied details belong to a merchant.
            var key = string.Concat(value.Where(char.IsLetterOrDigit)).ToLowerInvariant();
            return key is not ("" or "na" or "none" or "null" or "todo" or "tbd" or "changeme"
                or "placeholder" or "notconfigured" or "notset" or "example" or "sample" or "test"
                or "beneficiary" or "bankname" or "bank" or "name" or "yourname" or "yourbank"
                or "yourbankname" or "yourbeneficiary" or "testbank" or "testbeneficiary"
                or "examplebank" or "examplebeneficiary");
        }
    }
}
