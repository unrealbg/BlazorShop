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
                .OrderByDescending(product => product.CreatedOn)
                .Select(product => new Product
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    Image = product.Image,
                    Quantity = product.Quantity,
                    CreatedOn = product.CreatedOn,
                    CategoryId = product.CategoryId,
                })
                .ToListAsync();
        }

        public async Task<PagedResult<CatalogProductReadModel>> GetCatalogPageAsync(ProductCatalogQuery query)
        {
            var pageNumber = query.GetNormalizedPageNumber();
            var pageSize = query.GetNormalizedPageSize();
            var searchTerm = query.GetNormalizedSearchTerm();

            IQueryable<Product> products = _context.Products.AsNoTracking();

            if (query.CategoryId.HasValue && query.CategoryId.Value != Guid.Empty)
            {
                products = products.Where(product => product.CategoryId == query.CategoryId.Value);
            }

            if (query.CreatedAfterUtc.HasValue)
            {
                products = products.Where(product => product.CreatedOn >= query.CreatedAfterUtc.Value);
            }

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var normalizedSearchTerm = searchTerm.ToLower();
                products = products.Where(product =>
                    (product.Name != null && product.Name.ToLower().Contains(normalizedSearchTerm)) ||
                    (product.Description != null && product.Description.ToLower().Contains(normalizedSearchTerm)));
            }

            products = query.SortBy switch
            {
                ProductCatalogSortBy.Oldest => products.OrderBy(product => product.CreatedOn).ThenBy(product => product.Id),
                ProductCatalogSortBy.PriceLowToHigh => products.OrderBy(product => product.Price).ThenBy(product => product.Id),
                ProductCatalogSortBy.PriceHighToLow => products.OrderByDescending(product => product.Price).ThenBy(product => product.Id),
                ProductCatalogSortBy.NameAscending => products.OrderBy(product => product.Name).ThenBy(product => product.Id),
                ProductCatalogSortBy.NameDescending => products.OrderByDescending(product => product.Name).ThenBy(product => product.Id),
                _ => products.OrderByDescending(product => product.CreatedOn).ThenBy(product => product.Id),
            };

            var totalCount = await products.CountAsync();
            var items = await products
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(product => new CatalogProductReadModel
                {
                    Id = product.Id,
                    Name = product.Name,
                    Description = product.Description,
                    Price = product.Price,
                    Image = product.Image,
                    CreatedOn = product.CreatedOn,
                    CategoryId = product.CategoryId,
                    HasVariants = product.Variants.Any(),
                })
                .ToListAsync();

            return new PagedResult<CatalogProductReadModel>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
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