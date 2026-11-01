namespace BlazorShop.Testing;

using System.Globalization;
using System.Security.Cryptography;
using BlazorShop.Application.DTOs.Payment;

// Test-only, generated account with a synthetic bank identifier. Never submit a transfer.
internal static class SyntheticBankTransfer
{
    public static BankTransferSettings CreateSettings() => new()
    {
        Iban = CreateIban(),
        Beneficiary = "Synthetic regression merchant",
        BankName = "Synthetic regression institution",
        AdditionalInfo = "Use the order reference. Synthetic fixture only; never transfer funds.",
    };

    public static string CreateIban()
    {
        var bban = "ZZZZ000000" + RandomNumberGenerator.GetInt32(10_000_000, 100_000_000).ToString(CultureInfo.InvariantCulture);
        var remainder = 0;
        foreach (var character in bban + "BG00")
        {
            var number = character is >= 'A' and <= 'Z' ? character - 'A' + 10 : character - '0';
            remainder = (remainder * (number >= 10 ? 100 : 10) + number) % 97;
        }

        return "BG" + (98 - remainder).ToString("D2", CultureInfo.InvariantCulture) + bban;
    }
}
