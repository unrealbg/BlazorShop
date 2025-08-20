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
            var url = "https://www.paypal.com/checkoutnow?token=demo-token";
            return Task.FromResult(new ServiceResponse(true, url));
        }

        public Task<bool> CaptureAsync(string orderId)
        {
            return Task.FromResult(true);
        }
    }
}
