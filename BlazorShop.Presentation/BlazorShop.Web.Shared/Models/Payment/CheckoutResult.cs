namespace BlazorShop.Web.Shared.Models.Payment
{
    using System.Text.Json.Serialization;

    public sealed record CheckoutResult(
        Guid OrderId,
        string OrderReference,
        CheckoutStatus Status,
        CheckoutPaymentKind PaymentKind,
        string? RedirectUrl = null,
        BankTransferInfo? BankTransfer = null);

    public sealed record BankTransferInfo
    {
        public string Iban { get; init; } = string.Empty;

        public string Beneficiary { get; init; } = string.Empty;

        public string BankName { get; init; } = string.Empty;

        public string Reference { get; init; } = string.Empty;

        public decimal Amount { get; init; }

        public string Currency { get; init; } = string.Empty;

        public string? AdditionalInfo { get; init; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CheckoutStatus
    {
        Confirmed,
        PendingPayment,
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CheckoutPaymentKind
    {
        CashOnDelivery,
        BankTransfer,
        Stripe,
    }
}
