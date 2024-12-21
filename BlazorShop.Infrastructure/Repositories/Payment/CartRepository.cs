namespace BlazorShop.Infrastructure.Repositories.Payment
{
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    public class CartRepository : ICart
    {
        private readonly AppDbContext _context;

        public CartRepository(AppDbContext context)
        {
            this._context = context;
        }

        public async Task<int> SaveCheckoutHistory(IEnumerable<OrderItem> checkouts)
        {
            this._context.CheckoutOrderItems.AddRange(checkouts);
            return await _context.SaveChangesAsync();
        }

        public async Task<IEnumerable<OrderItem>> GetAllCheckoutHistory()
        {
            return await _context.CheckoutOrderItems.AsNoTracking().ToListAsync();
        }
    }
}
