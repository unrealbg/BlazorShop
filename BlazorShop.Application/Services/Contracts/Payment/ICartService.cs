namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;

    public interface ICartService
    {
        Task<ServiceResponse> SaveCheckoutHistoryAsync(IEnumerable<CreateAchieve> achieves);

        Task<ServiceResponse> CheckoutAsync(Checkout checkout);
    }
}
