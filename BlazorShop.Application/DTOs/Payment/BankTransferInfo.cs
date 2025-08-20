namespace BlazorShop.Application.DTOs.Payment
{
    public class BankTransferInfo
    {
        public string Iban { get; set; } = string.Empty;

        public string Beneficiary { get; set; } = string.Empty;

        public string BankName { get; set; } = string.Empty;

        public string Reference { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string? AdditionalInfo { get; set; }
    }
}
