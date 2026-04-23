namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;

    public interface ICartService
    {
        Task<ServiceResponse> SaveCheckoutHistoryAsync(string userId, IEnumerable<CreateOrderItem> orderItems);

        Task<ServiceResponse> ConfirmOrderAsync(IEnumerable<ProcessCart> carts, string userId, string? status = null);

        Task<ServiceResponse> CheckoutAsync(Checkout checkout);

        Task<ServiceResponse> CheckoutAsync(Checkout checkout, string? userId);

        Task<IEnumerable<GetOrderItem>> GetOrderItemsAsync();

        Task<IEnumerable<GetOrderItem>> GetCheckoutHistoryByUserId(string userId);
    }
}
