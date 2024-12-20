namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Payment;

    public interface ICartService
    {
        Task<ServiceResponse> Checkout(Checkout checkout);

        Task<ServiceResponse> SaveCheckoutHistory(IEnumerable<CreateOrderItem> orderItems);
    }
}
