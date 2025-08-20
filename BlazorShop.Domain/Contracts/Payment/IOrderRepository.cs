namespace BlazorShop.Domain.Contracts.Payment
{
    using BlazorShop.Domain.Entities.Payment;

    public interface IOrderRepository
    {
        Task<Guid> CreateAsync(Order order);

        Task<Order?> GetByReferenceAsync(string reference);

        Task<int> UpdateStatusAsync(Guid orderId, string status);
    }
}
