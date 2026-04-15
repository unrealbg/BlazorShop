namespace BlazorShop.Infrastructure.Repositories
{
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    public class ProductReadRepository : IProductReadRepository
    {
        private readonly AppDbContext _context;

        public ProductReadRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Product>> GetCatalogProductsAsync()
        {
            return await _context.Products
                .AsNoTracking()
                .Include(product => product.Category)
                .Include(product => product.Variants)
                .ToListAsync();
        }

        public async Task<Product?> GetProductDetailsByIdAsync(Guid id)
        {
            return await _context.Products
                .AsNoTracking()
                .Include(product => product.Category)
                .Include(product => product.Variants)
                .FirstOrDefaultAsync(product => product.Id == id);
        }

        public async Task<IReadOnlyDictionary<Guid, Product>> GetProductsByIdsAsync(IEnumerable<Guid> productIds)
        {
            var ids = productIds
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToArray();

            if (ids.Length == 0)
            {
                return new Dictionary<Guid, Product>();
            }

            return await _context.Products
                .AsNoTracking()
                .Where(product => ids.Contains(product.Id))
                .ToDictionaryAsync(product => product.Id);
        }
    }
}