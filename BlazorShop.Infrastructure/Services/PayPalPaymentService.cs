namespace BlazorShop.Infrastructure.Services
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    public class PayPalPaymentService : IPayPalPaymentService
    {
        public Task<ServiceResponse> Pay(IReadOnlyCollection<ResolvedCartLine> lines)
        {
            return Task.FromResult(new ServiceResponse(false, "PayPal payments are not currently available."));
        }

        public Task<bool> CaptureAsync(string orderId)
        {
            return Task.FromResult(false);
        }
    }
}
