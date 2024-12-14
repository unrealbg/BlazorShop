namespace BlazorShop.Domain.Contracts.Payment
{
    using BlazorShop.Domain.Entities.Payment;

    public interface ICart
    {
        Task<int> SaveCheckoutHistory(IEnumerable<Achieve> checkouts);
    }
}
