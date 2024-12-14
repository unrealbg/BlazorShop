namespace BlazorShop.Infrastructure.Repositories.Payment
{
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    public class PaymentMethodRepository : IPaymentMethod
    {
        private readonly AppDbContext _context;

        public PaymentMethodRepository(AppDbContext context)
        {
            this._context = context;
        }

        public async Task<IEnumerable<PaymentMethod>> GetPaymentMethodsAsync()
        {
            return await this._context.PaymentMethods.AsNoTracking().ToListAsync();
        }
    }
}
