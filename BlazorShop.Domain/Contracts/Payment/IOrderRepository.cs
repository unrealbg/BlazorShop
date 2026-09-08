namespace BlazorShop.Domain.Contracts.Payment
{
    using BlazorShop.Domain.Entities.Payment;

    public interface IOrderRepository
    {
        Task<Guid> CreateAsync(Order order);

        Task<Order?> GetByIdAsync(Guid orderId);

        Task<Order?> GetByReferenceAsync(string reference);

        Task<List<Order>> GetByUserIdAsync(string userId);

        Task<List<Order>> GetAllAsync();

        Task<List<Order>> GetByDateRangeAsync(DateTime fromUtc, DateTime toUtc);
    }
}
