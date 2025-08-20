namespace BlazorShop.Infrastructure.Repositories.Payment
{
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;
    using Microsoft.EntityFrameworkCore;

    public class OrderRepository : IOrderRepository
    {
        private readonly AppDbContext _context;
        public OrderRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Guid> CreateAsync(Order order)
        {
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return order.Id;
        }

        public async Task<Order?> GetByReferenceAsync(string reference)
        {
            return await _context.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Reference == reference);
        }

        public async Task<int> UpdateStatusAsync(Guid orderId, string status)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return 0;
            order.Status = status;
            return await _context.SaveChangesAsync();
        }
    }
}
