namespace BlazorShop.Application.DTOs.Payment
{
    public sealed record PaymentInitializationResult(
        bool Success,
        string? RedirectUrl = null,
        string? ErrorMessage = null,
        PaymentInitializationFailureKind FailureKind = PaymentInitializationFailureKind.None);

    public enum PaymentInitializationFailureKind
    {
        None,
        Definitive,
        Ambiguous,
    }
}
