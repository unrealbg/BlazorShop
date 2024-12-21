namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;

    public interface ICartService
    {
        Task<ServiceResponse> SaveCheckoutHistoryAsync(IEnumerable<CreateOrderItem> orderItems);

        Task<ServiceResponse> CheckoutAsync(Checkout checkout);

        Task<IEnumerable<GetOrderItem>> GetOrderItemsAsync();
    }
}
