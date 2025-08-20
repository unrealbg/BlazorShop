namespace BlazorShop.Application.DTOs.Payment
{
    public class BankTransferSettings
    {
        public string Iban { get; set; } = string.Empty;

        public string Beneficiary { get; set; } = string.Empty;

        public string BankName { get; set; } = string.Empty;

        public string? AdditionalInfo { get; set; }
    }
}
