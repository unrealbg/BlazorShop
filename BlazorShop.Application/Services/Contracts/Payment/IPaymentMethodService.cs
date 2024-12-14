namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Application.DTOs.Payment;

    public interface IPaymentMethodService
    {
        Task<IEnumerable<GetPaymentMethod>> GetPaymentMethodsAsync();
    }
}
