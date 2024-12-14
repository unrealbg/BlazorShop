namespace BlazorShop.Infrastructure.Repositories.Payment
{
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;

    public class CartRepository : ICart
    {
        private readonly AppDbContext _context;

        public CartRepository(AppDbContext context)
        {
            this._context = context;
        }

        public async Task<int> SaveCheckoutHistory(IEnumerable<Achieve> checkouts)
        {
            this._context.CheckoutAchieves.AddRange(checkouts);
            return await this._context.SaveChangesAsync();
        }
    }
}
