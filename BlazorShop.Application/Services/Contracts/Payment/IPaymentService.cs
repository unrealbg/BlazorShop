namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Application.DTOs.Payment;
    public interface IPaymentService
    {
        Task<PaymentInitializationResult> Pay(
            IReadOnlyCollection<ResolvedCartLine> lines,
            Guid orderId,
            string orderReference,
            string providerIdempotencyKey,
            CancellationToken cancellationToken = default);
    }
}
