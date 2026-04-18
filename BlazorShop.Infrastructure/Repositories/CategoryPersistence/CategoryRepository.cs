namespace BlazorShop.Infrastructure.Repositories.CategoryPersistence
{
    using BlazorShop.Domain.Contracts.CategoryPersistence;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    public class CategoryRepository : ICategoryRepository
    {
        private readonly AppDbContext _context;

        public CategoryRepository(AppDbContext dbContext)
        {
            _context = dbContext;
        }

        public async Task<IEnumerable<Product>> GetProductsByCategoryAsync(Guid categoryId)
        {
            var products = await _context
                               .Products
                               .Include(x => x.Category)
                               .Where(p => p.CategoryId == categoryId)
                               .AsNoTracking()
                               .ToListAsync();

            return products.Count > 0 ? products : [];
        }

        public async Task<bool> CategorySlugExistsAsync(string slug, Guid? excludedCategoryId = null)
        {
            return await _context.Categories
                .AsNoTracking()
                .AnyAsync(category => category.Slug == slug
                    && (!excludedCategoryId.HasValue || category.Id != excludedCategoryId.Value));
        }
    }
}
