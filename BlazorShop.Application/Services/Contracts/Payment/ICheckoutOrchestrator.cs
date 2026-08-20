namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;

    public interface ICheckoutOrchestrator
    {
        Task<CheckoutExecutionResult> CheckoutAsync(
            Checkout checkout,
            string userId,
            Guid idempotencyKey,
            CancellationToken cancellationToken = default);
    }
}
