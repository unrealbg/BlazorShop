namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Payment;

    public interface IPaymentMethodService
    {
        Task<QueryResult<IEnumerable<GetPaymentMethod>>> GetPaymentMethods();
    }
}
