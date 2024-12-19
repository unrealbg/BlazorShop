namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models.Payment;

    public interface IPaymentMethodService
    {
        Task<IEnumerable<GetPaymentMethod>> GetPaymentMethods();
    }
}
