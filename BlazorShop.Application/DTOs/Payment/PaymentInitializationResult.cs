namespace BlazorShop.Application.DTOs.Payment
{
    public sealed record PaymentInitializationResult(
        bool Success,
        string? RedirectUrl = null,
        string? ErrorMessage = null,
        PaymentInitializationFailureKind FailureKind = PaymentInitializationFailureKind.None,
        string? ProviderSessionId = null,
        string? ProviderPaymentIntentId = null);

    public enum PaymentInitializationFailureKind
    {
        None,
        Definitive,
        Ambiguous,
    }

    public sealed record StripePaymentRecoveryRequest(
        StripeCheckoutInitialization Initialization,
        string ProviderSessionId,
        string? ProviderPaymentIntentId);

    public sealed record StripePaymentRecoveryResult(
        StripePaymentRecoveryOutcome Outcome,
        string? ProviderPaymentIntentId = null,
        string? Reason = null);

    public enum StripePaymentRecoveryOutcome
    {
        Paid,
        Expired,
        Open,
        Unresolved,
    }
}
