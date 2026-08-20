namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;

    public interface ICheckoutOrchestrator
    {
        Task<ServiceResponse<CheckoutResult>> CheckoutAsync(
            Checkout checkout,
            string userId,
            CancellationToken cancellationToken = default);
    }
}
