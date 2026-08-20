namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    public interface IPaymentService
    {
        Task<ServiceResponse> Pay(IReadOnlyCollection<ResolvedCartLine> lines, Guid orderId);
    }
}
