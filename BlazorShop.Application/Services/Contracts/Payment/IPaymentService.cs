namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Application.DTOs.Payment;
    public interface IPaymentService
    {
        Task<PaymentInitializationResult> Pay(
            StripeCheckoutInitialization initialization,
            string providerIdempotencyKey,
            CancellationToken cancellationToken = default);
    }
}
