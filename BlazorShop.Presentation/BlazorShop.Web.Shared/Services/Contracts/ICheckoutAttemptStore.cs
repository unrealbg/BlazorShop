namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models.Payment;

    public interface ICheckoutAttemptStore
    {
        Task<CheckoutAttempt> GetOrCreateAsync(Checkout checkout);

        Task ClearAsync(Guid completedKey);
    }
}
