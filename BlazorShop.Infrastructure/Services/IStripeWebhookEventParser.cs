namespace BlazorShop.Infrastructure.Services
{
    public interface IStripeWebhookEventParser
    {
        StripeWebhookEventData Parse(string payload, string signature, string webhookSecret);
    }

    public sealed record StripeWebhookEventData(
        string EventId,
        string EventType,
        DateTime ProviderCreatedOn,
        string? SessionId,
        string? PaymentIntentId,
        Guid? OrderId,
        string? ClientReferenceId,
        Guid? PaymentTransactionId,
        string? PaymentStatus,
        long? AmountTotal,
        string? Currency,
        string? SessionStatus);

    public sealed class StripeWebhookSignatureException : Exception
    {
        public StripeWebhookSignatureException(Exception innerException)
            : base("The Stripe webhook signature or payload is invalid.", innerException)
        {
        }
    }
}
