namespace BlazorShop.Domain.Contracts.Payment
{
    using BlazorShop.Domain.Entities.Payment;

    public interface IPaymentMethod
    {
        Task<IEnumerable<PaymentMethod>> GetPaymentMethodsAsync();
    }
}
