namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Domain.Contracts.Payment;

    public interface ICartService
    {
        Task<ServiceResponse> SaveCheckoutHistoryAsync(string userId, IEnumerable<CreateOrderItem> orderItems);

        Task<ServiceResponse> ConfirmOrderAsync(IEnumerable<CartLineRequest> carts, string userId);

        Task<ServiceResponse> CheckoutAsync(Checkout checkout);

        Task<ServiceResponse> CheckoutAsync(Checkout checkout, string? userId);

        Task<IEnumerable<GetOrderItem>> GetOrderItemsAsync();

        Task<IEnumerable<GetOrderItem>> GetCheckoutHistoryByUserId(string userId);
    }
}
