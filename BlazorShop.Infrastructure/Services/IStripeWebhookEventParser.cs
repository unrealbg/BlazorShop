namespace BlazorShop.Infrastructure.Services
{
    public interface IStripeWebhookEventParser
    {
        StripeWebhookEventData Parse(string payload, string signature, string webhookSecret);
    }

    public sealed record StripeWebhookEventData(
        string EventId,
        string EventType,
        Guid? OrderId,
        string? PaymentStatus);

    public sealed class StripeWebhookSignatureException : Exception
    {
        public StripeWebhookSignatureException(Exception innerException)
            : base("The Stripe webhook signature or payload is invalid.", innerException)
        {
        }
    }
}
