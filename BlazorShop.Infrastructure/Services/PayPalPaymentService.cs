namespace BlazorShop.Infrastructure.Services
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Entities;

    public class PayPalPaymentService : IPayPalPaymentService
    {
        public Task<ServiceResponse> Pay(decimal totalAmount, IEnumerable<Product> cartProducts, IEnumerable<ProcessCart> carts)
        {
            return Task.FromResult(new ServiceResponse(false, "PayPal payments are not currently available."));
        }

        public Task<bool> CaptureAsync(string orderId)
        {
            return Task.FromResult(false);
        }
    }
}
