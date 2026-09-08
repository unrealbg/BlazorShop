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

        public async Task<Order?> GetByIdAsync(Guid orderId)
        {
            return await _context.Orders
                .Include(order => order.Lines)
                .FirstOrDefaultAsync(order => order.Id == orderId);
        }

        public async Task<Order?> GetByReferenceAsync(string reference)
        {
            return await _context.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Reference == reference);
        }

        public async Task<List<Order>> GetByUserIdAsync(string userId)
        {
            return await _context.Orders.Include(o => o.Lines).Where(o => o.UserId == userId).OrderByDescending(o => o.CreatedOn).ToListAsync();
        }

        public async Task<List<Order>> GetAllAsync()
        {
            return await _context.Orders.Include(o => o.Lines).OrderByDescending(o => o.CreatedOn).ToListAsync();
        }

        public async Task<List<Order>> GetByDateRangeAsync(DateTime fromUtc, DateTime toUtc)
        {
            return await _context.Orders
                .Include(o => o.Lines)
                .Where(o => o.CreatedOn >= fromUtc && o.CreatedOn <= toUtc)
                .OrderBy(o => o.CreatedOn)
                .ToListAsync();
        }
    }
}
