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

        Task<PaymentTransaction?> GetStripeByOrderIdAsync(
            Guid orderId,
            CancellationToken cancellationToken = default);

        Task<PaymentProviderIdentityPersistenceResult> PersistProviderIdentityAsync(
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

    public sealed record PaymentProviderIdentityPersistenceResult(
        PaymentProviderIdentityPersistenceOutcome Outcome,
        PaymentTransactionStatus? PaymentStatus);
}
