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
                    Slug = product.Slug,
                    MetaTitle = product.MetaTitle,
                    MetaDescription = product.MetaDescription,
                    CanonicalUrl = product.CanonicalUrl,
                    OgTitle = product.OgTitle,
                    OgDescription = product.OgDescription,
                    OgImage = product.OgImage,
                    RobotsIndex = product.RobotsIndex,
                    RobotsFollow = product.RobotsFollow,
                    SeoContent = product.SeoContent,
                    IsPublished = product.IsPublished,
                    PublishedOn = product.PublishedOn,
                    CategoryId = product.CategoryId,
                })
                .ToListAsync();
        }

        public async Task<PagedResult<CatalogProductReadModel>> GetCatalogPageAsync(ProductCatalogQuery query)
        {
            var pageNumber = query.GetNormalizedPageNumber();
            var pageSize = query.GetNormalizedPageSize();
            IQueryable<Product> products = BuildCatalogQuery(_context.Products.AsNoTracking(), query);

            var totalCount = await products.CountAsync();
            var items = await products
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(MapCatalogProduct())
                .ToListAsync();

            return new PagedResult<CatalogProductReadModel>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
        }

        public async Task<PagedResult<CatalogProductReadModel>> GetPublishedCatalogPageAsync(ProductCatalogQuery query)
        {
            var pageNumber = query.GetNormalizedPageNumber();
            var pageSize = query.GetNormalizedPageSize();

            IQueryable<Product> products = BuildCatalogQuery(
                _context.Products
                    .AsNoTracking()
                    .Where(product => product.IsPublished
                        && product.PublishedOn != null
                        && product.Slug != null
                        && product.Slug != string.Empty
                        && product.Category != null
                        && product.Category.IsPublished),
                query);

            var totalCount = await products.CountAsync();
            var items = await products
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(MapCatalogProduct())
                .ToListAsync();

            return new PagedResult<CatalogProductReadModel>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
            };
        }

        public async Task<IReadOnlyList<PublishedProductSitemapEntryReadModel>> GetPublishedProductSitemapEntriesAsync()
        {
            return await _context.Products
                .AsNoTracking()
                .Where(product => product.IsPublished
                    && product.PublishedOn != null
                    && product.Slug != null
                    && product.Slug != string.Empty
                    && product.Category != null
                    && product.Category.IsPublished)
                .OrderBy(product => product.PublishedOn)
                .ThenBy(product => product.Id)
                .Select(product => new PublishedProductSitemapEntryReadModel
                {
                    Slug = product.Slug!,
                    LastModifiedUtc = product.PublishedOn ?? product.CreatedOn,
                })
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

        public async Task<Product?> GetPublishedProductBySlugAsync(string slug)
        {
            return await _context.Products
                .AsNoTracking()
                .Include(product => product.Category)
                .Include(product => product.Variants)
                .FirstOrDefaultAsync(product => product.IsPublished
                    && product.PublishedOn != null
                    && product.Slug == slug
                    && product.Category != null
                    && product.Category.IsPublished);
        }

        public async Task<IReadOnlyList<CatalogProductReadModel>> GetPublishedProductsByCategoryAsync(Guid categoryId)
        {
            return await _context.Products
                .AsNoTracking()
                .Where(product => product.CategoryId == categoryId
                    && product.IsPublished
                    && product.PublishedOn != null
                    && product.Slug != null
                    && product.Slug != string.Empty)
                .OrderByDescending(product => product.CreatedOn)
                .ThenBy(product => product.Id)
                .Select(MapCatalogProduct())
                .ToListAsync();
        }

        public async Task<bool> ProductSlugExistsAsync(string slug, Guid? excludedProductId = null)
        {
            return await _context.Products
                .AsNoTracking()
                .AnyAsync(product => product.Slug == slug
                    && (!excludedProductId.HasValue || product.Id != excludedProductId.Value));
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

        private static IQueryable<Product> BuildCatalogQuery(IQueryable<Product> products, ProductCatalogQuery query)
        {
            var searchTerm = query.GetNormalizedSearchTerm();

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

            return query.SortBy switch
            {
                ProductCatalogSortBy.Oldest => products.OrderBy(product => product.CreatedOn).ThenBy(product => product.Id),
                ProductCatalogSortBy.PriceLowToHigh => products.OrderBy(product => product.Price).ThenBy(product => product.Id),
                ProductCatalogSortBy.PriceHighToLow => products.OrderByDescending(product => product.Price).ThenBy(product => product.Id),
                ProductCatalogSortBy.NameAscending => products.OrderBy(product => product.Name).ThenBy(product => product.Id),
                ProductCatalogSortBy.NameDescending => products.OrderByDescending(product => product.Name).ThenBy(product => product.Id),
                _ => products.OrderByDescending(product => product.CreatedOn).ThenBy(product => product.Id),
            };
        }

        private static System.Linq.Expressions.Expression<Func<Product, CatalogProductReadModel>> MapCatalogProduct()
        {
            return product => new CatalogProductReadModel
            {
                Id = product.Id,
                Slug = product.IsPublished ? product.Slug : null,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Image = product.Image,
                CreatedOn = product.CreatedOn,
                CategoryId = product.CategoryId,
                CategoryName = product.Category != null ? product.Category.Name : null,
                CategorySlug = product.Category != null && product.Category.IsPublished ? product.Category.Slug : null,
                HasVariants = product.Variants.Any(),
            };
        }
    }
}