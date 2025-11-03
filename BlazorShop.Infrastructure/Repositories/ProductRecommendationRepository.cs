namespace BlazorShop.Infrastructure.Repositories
{
    using BlazorShop.Application.Services.Contracts.Logging;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    public class ProductRecommendationRepository : IProductRecommendationRepository
    {
        private readonly AppDbContext _context;
        private readonly IAppLogger<ProductRecommendationRepository> _logger;

        public ProductRecommendationRepository(
            AppDbContext context,
            IAppLogger<ProductRecommendationRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Product>> GetRelatedProductsByCategoryAsync(
            Guid productId,
            Guid categoryId,
            int count = 4)
        {
            try
            {
                _logger.LogInformation($"Fetching {count} related products for product {productId} in category {categoryId}");

                var products = await _context.Products
                    .AsNoTracking()
                    .Include(p => p.Category)
                    .Include(p => p.Variants)
                    .Where(p =>
                        p.CategoryId == categoryId &&
                        p.Id != productId &&
                        p.Quantity > 0)
                    .OrderByDescending(p => p.CreatedOn)
                    .Take(count)
                    .ToListAsync();

                _logger.LogInformation($"Found {products.Count} related products");
                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching related products");
                return Enumerable.Empty<Product>();
            }
        }

        public async Task<IEnumerable<Product>> GetFrequentlyBoughtTogetherAsync(Guid productId, int count = 4)
        {
            try
            {
                _logger.LogInformation($"Fetching frequently bought together products for product {productId}");

                var relatedProductIds = await _context.OrderLines
                    .AsNoTracking()
                    .Where(ol => _context.OrderLines
                        .Any(ol2 => ol2.OrderId == ol.OrderId && ol2.ProductId == productId))
                    .Where(ol => ol.ProductId != productId)
                    .GroupBy(ol => ol.ProductId)
                    .OrderByDescending(g => g.Count()) 
                    .Select(g => g.Key)
                    .Take(count)
                    .ToListAsync();

                if (!relatedProductIds.Any())
                {
                    _logger.LogInformation("No order history found, falling back to category-based recommendations");

                    var product = await _context.Products
                        .AsNoTracking()
                        .FirstOrDefaultAsync(p => p.Id == productId);

                    if (product == null)
                    {
                        _logger.LogWarning($"Product {productId} not found");
                        return Enumerable.Empty<Product>();
                    }

                    return await GetRelatedProductsByCategoryAsync(productId, product.CategoryId, count);
                }

                var products = await _context.Products
                    .AsNoTracking()
                    .Include(p => p.Category)
                    .Include(p => p.Variants)
                    .Where(p => relatedProductIds.Contains(p.Id) && p.Quantity > 0)
                    .ToListAsync();

                _logger.LogInformation($"Found {products.Count} frequently bought together products");
                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching frequently bought together products");
                return Enumerable.Empty<Product>();
            }
        }

        public async Task<IEnumerable<Product>> GetRecentlyViewedProductsAsync(
            IEnumerable<Guid> productIds,
            int count = 4)
        {
            try
            {
                if (productIds == null || !productIds.Any())
                {
                    _logger.LogInformation("No product IDs provided for recently viewed");
                    return Enumerable.Empty<Product>();
                }

                _logger.LogInformation($"Fetching {count} recently viewed products");

                var productIdsList = productIds.ToList();

                var products = await _context.Products
                    .AsNoTracking()
                    .Include(p => p.Category)
                    .Include(p => p.Variants)
                    .Where(p => productIdsList.Contains(p.Id) && p.Quantity > 0)
                    .OrderByDescending(p => p.CreatedOn)
                    .Take(count)
                    .ToListAsync();

                _logger.LogInformation($"Found {products.Count} recently viewed products");
                return products;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error fetching recently viewed products");
                return Enumerable.Empty<Product>();
            }
        }
    }
}
