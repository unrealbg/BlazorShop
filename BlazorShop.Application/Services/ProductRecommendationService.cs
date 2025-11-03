namespace BlazorShop.Application.Services
{
    using AutoMapper;

    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Application.Services.Contracts.Logging;
    using BlazorShop.Domain.Contracts;

    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Options;

    public class ProductRecommendationService : IProductRecommendationService
    {
        private readonly IProductRecommendationRepository _recommendationRepository;
        private readonly IGenericRepository<Domain.Entities.Product> _productRepository;
        private readonly IMapper _mapper;
        private readonly IMemoryCache _cache;
        private readonly IAppLogger<ProductRecommendationService> _logger;
        private readonly RecommendationOptions _options;

        private const string CacheKeyPrefix = "ProductRecommendations_";

        public ProductRecommendationService(
            IProductRecommendationRepository recommendationRepository,
            IGenericRepository<Domain.Entities.Product> productRepository,
            IMapper mapper,
            IMemoryCache cache,
            IAppLogger<ProductRecommendationService> logger,
            IOptions<RecommendationOptions> options)
        {
            _recommendationRepository = recommendationRepository;
            _productRepository = productRepository;
            _mapper = mapper;
            _cache = cache;
            _logger = logger;
            _options = options.Value;
        }

        public async Task<IEnumerable<GetProductRecommendation>> GetRecommendationsForProductAsync(Guid productId)
        {
            try
            {
                if (productId == Guid.Empty)
                {
                    _logger.LogWarning("Invalid product ID provided");
                    return Enumerable.Empty<GetProductRecommendation>();
                }

                string cacheKey = $"{CacheKeyPrefix}{productId}";

                if (_cache.TryGetValue(cacheKey, out IEnumerable<GetProductRecommendation>? cachedRecommendations))
                {
                    _logger.LogInformation($"Returning cached recommendations for product {productId}");
                    return cachedRecommendations ?? Enumerable.Empty<GetProductRecommendation>();
                }

                _logger.LogInformation($"Cache miss, fetching recommendations for product {productId}");

                var product = await _productRepository.GetByIdAsync(productId);
                if (product == null)
                {
                    _logger.LogWarning($"Product {productId} not found");
                    return Enumerable.Empty<GetProductRecommendation>();
                }

                IEnumerable<Domain.Entities.Product> relatedProducts;

                if (_options.EnableOrderBasedRecommendations)
                {
                    relatedProducts = await _recommendationRepository
                      .GetFrequentlyBoughtTogetherAsync(productId, _options.MaxRecommendations);
                }
                else
                {
                    relatedProducts = Enumerable.Empty<Domain.Entities.Product>();
                }

                if (!relatedProducts.Any())
                {
                    _logger.LogInformation("No order-based recommendations, using category-based");
                    relatedProducts = await _recommendationRepository
                      .GetRelatedProductsByCategoryAsync(productId, product.CategoryId, _options.MaxRecommendations);
                }

                var recommendations = _mapper.Map<IEnumerable<GetProductRecommendation>>(relatedProducts);

                if (recommendations.Any())
                {
                    var cacheOptions = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_options.CacheDurationHours),
                        SlidingExpiration = TimeSpan.FromMinutes(_options.SlidingExpirationMinutes),
                        Priority = CacheItemPriority.Normal
                    };

                    _cache.Set(cacheKey, recommendations, cacheOptions);
                    _logger.LogInformation($"Cached {recommendations.Count()} recommendations for product {productId}");
                }
                else
                {
                    _logger.LogInformation($"No recommendations found for product {productId}");
                }

                return recommendations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting recommendations for product {productId}");
                return Enumerable.Empty<GetProductRecommendation>();
            }
        }
    }
}
