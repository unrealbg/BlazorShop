namespace BlazorShop.Application.Services
{
    using AutoMapper;

    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.DTOs.Discovery;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.CategoryPersistence;

    public class PublicCatalogService : IPublicCatalogService
    {
        private readonly ICategoryRepository _categoryRepository;
        private readonly IMapper _mapper;
        private readonly IProductReadRepository _productReadRepository;
        private readonly ISlugService _slugService;

        public PublicCatalogService(
            ICategoryRepository categoryRepository,
            IMapper mapper,
            IProductReadRepository productReadRepository,
            ISlugService slugService)
        {
            _categoryRepository = categoryRepository;
            _mapper = mapper;
            _productReadRepository = productReadRepository;
            _slugService = slugService;
        }

        public async Task<IEnumerable<GetCategory>> GetPublishedCategoriesAsync()
        {
            var categories = await _categoryRepository.GetPublishedCategoriesAsync();
            return categories.Any() ? _mapper.Map<IEnumerable<GetCategory>>(categories) : [];
        }

        public async Task<GetPublicCatalogSitemap> GetPublishedSitemapAsync()
        {
            var categoryTask = _categoryRepository.GetPublishedCategorySitemapEntriesAsync();
            var productTask = _productReadRepository.GetPublishedProductSitemapEntriesAsync();

            await Task.WhenAll(categoryTask, productTask);

            return new GetPublicCatalogSitemap
            {
                Categories = categoryTask.Result
                    .Select(category => new GetCategorySitemapEntry
                    {
                        Slug = category.Slug,
                        LastModifiedUtc = category.LastModifiedUtc,
                    })
                    .ToArray(),
                Products = productTask.Result
                    .Select(product => new GetProductSitemapEntry
                    {
                        Slug = product.Slug,
                        LastModifiedUtc = product.LastModifiedUtc,
                    })
                    .ToArray(),
            };
        }

        public async Task<PagedResult<GetCatalogProduct>> GetPublishedCatalogPageAsync(ProductCatalogQuery query)
        {
            var result = await _productReadRepository.GetPublishedCatalogPageAsync(query);
            var mappedItems = _mapper.Map<IReadOnlyList<GetCatalogProduct>>(result.Items);

            return new PagedResult<GetCatalogProduct>
            {
                Items = mappedItems,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount,
            };
        }

        public async Task<GetProduct?> GetPublishedProductBySlugAsync(string slug)
        {
            var normalizedSlug = NormalizeSlug(slug);
            if (normalizedSlug is null)
            {
                return null;
            }

            var product = await _productReadRepository.GetPublishedProductBySlugAsync(normalizedSlug);
            return product is null ? null : _mapper.Map<GetProduct>(product);
        }

        public async Task<GetCategoryPage?> GetPublishedCategoryPageBySlugAsync(string slug)
        {
            var normalizedSlug = NormalizeSlug(slug);
            if (normalizedSlug is null)
            {
                return null;
            }

            var category = await _categoryRepository.GetPublishedCategoryBySlugAsync(normalizedSlug);
            if (category is null)
            {
                return null;
            }

            var products = await _productReadRepository.GetPublishedProductsByCategoryAsync(category.Id);

            return new GetCategoryPage
            {
                Category = _mapper.Map<GetCategory>(category),
                Products = _mapper.Map<IReadOnlyList<GetCatalogProduct>>(products),
            };
        }

        private string? NormalizeSlug(string slug)
        {
            var normalizedSlug = _slugService.NormalizeSlug(slug);
            return string.IsNullOrWhiteSpace(normalizedSlug) ? null : normalizedSlug;
        }
    }
}