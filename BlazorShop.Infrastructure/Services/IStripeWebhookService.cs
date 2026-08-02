namespace BlazorShop.Infrastructure.Services
{
    public interface IStripeWebhookService
    {
        Task<StripeWebhookHandlingResult> HandleAsync(
            string payload,
            string signature,
            CancellationToken cancellationToken = default);
    }

    public enum StripeWebhookHandlingResult
    {
        Processed,
        Ignored,
        InvalidSignature,
        InvalidPayload,
        OrderNotFound,
    }
}
