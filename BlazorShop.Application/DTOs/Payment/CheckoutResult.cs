namespace BlazorShop.Application.DTOs.Payment
{
    using System.Text.Json.Serialization;

    public sealed record CheckoutResult(
        Guid OrderId,
        string OrderReference,
        CheckoutStatus Status,
        CheckoutPaymentKind PaymentKind,
        string? RedirectUrl = null,
        BankTransferInfo? BankTransfer = null);

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
