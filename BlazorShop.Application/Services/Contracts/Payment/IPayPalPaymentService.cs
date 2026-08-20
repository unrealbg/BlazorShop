namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    public interface IPayPalPaymentService
    {
        Task<ServiceResponse> Pay(IReadOnlyCollection<ResolvedCartLine> lines);

        Task<bool> CaptureAsync(string orderId);
    }
}
