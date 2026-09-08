namespace BlazorShop.Infrastructure.Services
{
    public interface IStripePaymentReconciliationService
    {
        Task<StripePaymentReconciliationOutcome> ReconcileAsync(
            StripeWebhookEventData providerEvent,
            CancellationToken cancellationToken = default);
    }

    public enum StripePaymentReconciliationOutcome
    {
        Processed,
        Duplicate,
        Rejected,
        Ignored,
    }
}
