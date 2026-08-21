namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Domain.Entities.Payment;

    public interface IPaymentTransactionStore
    {
        Task<PaymentTransaction> GetOrCreateStripeAsync(
            Guid orderId,
            long expectedAmountMinor,
            string currency,
            CancellationToken cancellationToken = default);

        Task<PaymentProviderIdentityPersistenceOutcome> PersistProviderIdentityAsync(
            Guid paymentTransactionId,
            string providerSessionId,
            string? providerPaymentIntentId,
            CancellationToken cancellationToken = default);
    }

    public enum PaymentProviderIdentityPersistenceOutcome
    {
        Persisted,
        AlreadyPersisted,
        NotFound,
        Conflict,
    }
}
