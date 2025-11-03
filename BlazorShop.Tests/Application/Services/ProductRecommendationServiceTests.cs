namespace BlazorShop.Tests.Application.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using AutoMapper;

    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services;
    using BlazorShop.Application.Services.Contracts.Logging;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;

    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Options;

    using Moq;

    using Xunit;

    public class ProductRecommendationServiceTests
    {
        private readonly Mock<IProductRecommendationRepository> _mockRecommendationRepo;
        private readonly Mock<IGenericRepository<Product>> _mockProductRepo;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IMemoryCache> _mockCache;
        private readonly Mock<IAppLogger<ProductRecommendationService>> _mockLogger;
        private readonly IOptions<RecommendationOptions> _options;
        private readonly ProductRecommendationService _service;

        public ProductRecommendationServiceTests()
        {
            _mockRecommendationRepo = new Mock<IProductRecommendationRepository>();
            _mockProductRepo = new Mock<IGenericRepository<Product>>();
            _mockMapper = new Mock<IMapper>();
            _mockCache = new Mock<IMemoryCache>();
            _mockLogger = new Mock<IAppLogger<ProductRecommendationService>>();
            
            _options = Options.Create(new RecommendationOptions
            {
                MaxRecommendations = 6,
                CacheDurationHours = 1,
                SlidingExpirationMinutes = 30,
                EnableOrderBasedRecommendations = true,
                MinimumOrderCount = 5
            });

            _service = new ProductRecommendationService(
                _mockRecommendationRepo.Object,
                _mockProductRepo.Object,
                _mockMapper.Object,
                _mockCache.Object,
                _mockLogger.Object,
                _options);
        }

        [Fact]
        public void ServiceCanBeInstantiated()
        {
            // This test verifies that the service can be created
            // Full integration tests would require more setup
            Assert.NotNull(_mockRecommendationRepo);
            Assert.NotNull(_mockProductRepo);
            Assert.NotNull(_mockMapper);
            Assert.NotNull(_mockCache);
            Assert.NotNull(_mockLogger);
        }

        [Fact]
        public async Task GetRecommendationsForProductAsync_WithValidProductId_ReturnsRecommendations()
        {
            // Arrange
            var productId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var product = new Product 
            { 
                Id = productId, 
                CategoryId = categoryId,
                Name = "Test Product",
                Price = 100
            };
     
            var relatedProducts = new List<Product>
            {
                new Product { Id = Guid.NewGuid(), Name = "Product 1", Price = 10, CategoryId = categoryId },
                new Product { Id = Guid.NewGuid(), Name = "Product 2", Price = 20, CategoryId = categoryId }
            };
     
            var recommendations = new List<GetProductRecommendation>
            {
                new GetProductRecommendation { Id = relatedProducts[0].Id, Name = "Product 1", Price = 10 },
                new GetProductRecommendation { Id = relatedProducts[1].Id, Name = "Product 2", Price = 20 }
            };

            object? cachedValue = null;
            _mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cachedValue))
             .Returns(false);

            _mockProductRepo.Setup(x => x.GetByIdAsync(productId))
       .ReturnsAsync(product);

            _mockRecommendationRepo.Setup(x => x.GetFrequentlyBoughtTogetherAsync(productId, 6))
      .ReturnsAsync(relatedProducts);

            _mockMapper.Setup(x => x.Map<IEnumerable<GetProductRecommendation>>(relatedProducts))
     .Returns(recommendations);

            var mockCacheEntry = new Mock<ICacheEntry>();
            _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>()))
           .Returns(mockCacheEntry.Object);

     // Act
            var result = await _service.GetRecommendationsForProductAsync(productId);

    // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            Assert.Equal("Product 1", result.First().Name);
            _mockProductRepo.Verify(x => x.GetByIdAsync(productId), Times.Once);
            _mockRecommendationRepo.Verify(x => x.GetFrequentlyBoughtTogetherAsync(productId, 6), Times.Once);
            _mockLogger.Verify(x => x.LogInformation(It.IsAny<string>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetRecommendationsForProductAsync_WithEmptyGuid_ReturnsEmptyList()
         {
            // Act
            var result = await _service.GetRecommendationsForProductAsync(Guid.Empty);

          // Assert
           Assert.NotNull(result);
         Assert.Empty(result);
         _mockProductRepo.Verify(x => x.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
           _mockLogger.Verify(x => x.LogWarning("Invalid product ID provided"), Times.Once);
        }

        [Fact]
        public async Task GetRecommendationsForProductAsync_WithNonExistentProduct_ReturnsEmptyList()
        {
            // Arrange
            var productId = Guid.NewGuid();
            object? cachedValue = null;
    _mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cachedValue))
    .Returns(false);

        _mockProductRepo.Setup(x => x.GetByIdAsync(productId))
    .ReturnsAsync((Product)null!);

      // Act
    var result = await _service.GetRecommendationsForProductAsync(productId);

    // Assert
       Assert.NotNull(result);
Assert.Empty(result);
     _mockProductRepo.Verify(x => x.GetByIdAsync(productId), Times.Once);
      _mockLogger.Verify(x => x.LogWarning(It.Is<string>(s => s.Contains("not found"))), Times.Once);
        }

        [Fact]
        public async Task GetRecommendationsForProductAsync_UsesCachedValue_WhenAvailable()
        {
            // Arrange
       var productId = Guid.NewGuid();
    var cachedRecommendations = new List<GetProductRecommendation>
  {
         new GetProductRecommendation { Id = Guid.NewGuid(), Name = "Cached Product", Price = 15 }
            };

   object? cachedValue = cachedRecommendations;
         _mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cachedValue))
  .Returns(true);

            // Act
        var result = await _service.GetRecommendationsForProductAsync(productId);

 // Assert
        Assert.NotNull(result);
       Assert.Single(result);
         Assert.Equal("Cached Product", result.First().Name);
  _mockProductRepo.Verify(x => x.GetByIdAsync(It.IsAny<Guid>()), Times.Never);
        _mockLogger.Verify(x => x.LogInformation(It.Is<string>(s => s.Contains("cached"))), Times.Once);
    }

        [Fact]
        public async Task GetRecommendationsForProductAsync_FallsBackToCategoryBased_WhenNoOrderHistory()
        {
  // Arrange
        var productId = Guid.NewGuid();
      var categoryId = Guid.NewGuid();
            var product = new Product { Id = productId, CategoryId = categoryId };
            var categoryProducts = new List<Product>
      {
      new Product { Id = Guid.NewGuid(), Name = "Category Product", Price = 25, CategoryId = categoryId }
            };
            var recommendations = new List<GetProductRecommendation>
            {
              new GetProductRecommendation { Id = categoryProducts[0].Id, Name = "Category Product", Price = 25 }
         };

    object? cachedValue = null;
 _mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cachedValue))
  .Returns(false);

     _mockProductRepo.Setup(x => x.GetByIdAsync(productId))
          .ReturnsAsync(product);

            _mockRecommendationRepo.Setup(x => x.GetFrequentlyBoughtTogetherAsync(productId, 6))
    .ReturnsAsync(new List<Product>());

        _mockRecommendationRepo.Setup(x => x.GetRelatedProductsByCategoryAsync(productId, categoryId, 6))
     .ReturnsAsync(categoryProducts);

 _mockMapper.Setup(x => x.Map<IEnumerable<GetProductRecommendation>>(categoryProducts))
           .Returns(recommendations);

 var mockCacheEntry = new Mock<ICacheEntry>();
_mockCache.Setup(x => x.CreateEntry(It.IsAny<object>()))
      .Returns(mockCacheEntry.Object);

            // Act
  var result = await _service.GetRecommendationsForProductAsync(productId);

            // Assert
       Assert.NotNull(result);
    Assert.Single(result);
          Assert.Equal("Category Product", result.First().Name);
   _mockRecommendationRepo.Verify(x => x.GetRelatedProductsByCategoryAsync(productId, categoryId, 6), Times.Once);
            _mockLogger.Verify(x => x.LogInformation(It.Is<string>(s => s.Contains("category-based"))), Times.Once);
        }

   [Fact]
        public async Task GetRecommendationsForProductAsync_HandlesException_ReturnsEmptyList()
        {
    // Arrange
            var productId = Guid.NewGuid();
     object? cachedValue = null;
            _mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cachedValue))
        .Returns(false);

         _mockProductRepo.Setup(x => x.GetByIdAsync(productId))
             .ThrowsAsync(new Exception("Database error"));

      // Act
var result = await _service.GetRecommendationsForProductAsync(productId);

            // Assert
  Assert.NotNull(result);
          Assert.Empty(result);
            _mockLogger.Verify(x => x.LogError(It.IsAny<Exception>(), It.IsAny<string>()), Times.Once);
  }

        [Fact]
        public async Task GetRecommendationsForProductAsync_CachesResults_WhenRecommendationsFound()
        {
            // Arrange
            var productId = Guid.NewGuid();
   var categoryId = Guid.NewGuid();
      var product = new Product { Id = productId, CategoryId = categoryId };
            var relatedProducts = new List<Product>
            {
     new Product { Id = Guid.NewGuid(), Name = "Product 1", Price = 10 }
        };
       var recommendations = new List<GetProductRecommendation>
        {
      new GetProductRecommendation { Id = relatedProducts[0].Id, Name = "Product 1", Price = 10 }
            };

          object? cachedValue = null;
            _mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cachedValue))
  .Returns(false);

          _mockProductRepo.Setup(x => x.GetByIdAsync(productId))
   .ReturnsAsync(product);

        _mockRecommendationRepo.Setup(x => x.GetFrequentlyBoughtTogetherAsync(productId, 6))
         .ReturnsAsync(relatedProducts);

         _mockMapper.Setup(x => x.Map<IEnumerable<GetProductRecommendation>>(relatedProducts))
     .Returns(recommendations);

     var mockCacheEntry = new Mock<ICacheEntry>();
   _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>()))
           .Returns(mockCacheEntry.Object);

          // Act
    var result = await _service.GetRecommendationsForProductAsync(productId);

    // Assert
Assert.NotNull(result);
  Assert.Single(result);
            _mockCache.Verify(x => x.CreateEntry(It.IsAny<object>()), Times.Once);
_mockLogger.Verify(x => x.LogInformation(It.Is<string>(s => s.Contains("Cached"))), Times.Once);
        }

      [Fact]
        public async Task GetRecommendationsForProductAsync_WithOrderBasedDisabled_UsesOnlyCategoryBased()
    {
 // Arrange
            var disabledOptions = Options.Create(new RecommendationOptions
      {
    MaxRecommendations = 6,
       EnableOrderBasedRecommendations = false
         });

          var service = new ProductRecommendationService(
                _mockRecommendationRepo.Object,
      _mockProductRepo.Object,
           _mockMapper.Object,
       _mockCache.Object,
            _mockLogger.Object,
            disabledOptions);

            var productId = Guid.NewGuid();
          var categoryId = Guid.NewGuid();
            var product = new Product { Id = productId, CategoryId = categoryId };
       var categoryProducts = new List<Product>
            {
    new Product { Id = Guid.NewGuid(), Name = "Category Product", Price = 25 }
   };
      var recommendations = new List<GetProductRecommendation>
          {
       new GetProductRecommendation { Id = categoryProducts[0].Id, Name = "Category Product", Price = 25 }
            };

     object? cachedValue = null;
            _mockCache.Setup(x => x.TryGetValue(It.IsAny<object>(), out cachedValue))
   .Returns(false);

            _mockProductRepo.Setup(x => x.GetByIdAsync(productId))
     .ReturnsAsync(product);

            _mockRecommendationRepo.Setup(x => x.GetRelatedProductsByCategoryAsync(productId, categoryId, 6))
    .ReturnsAsync(categoryProducts);

   _mockMapper.Setup(x => x.Map<IEnumerable<GetProductRecommendation>>(categoryProducts))
                .Returns(recommendations);

      var mockCacheEntry = new Mock<ICacheEntry>();
            _mockCache.Setup(x => x.CreateEntry(It.IsAny<object>()))
     .Returns(mockCacheEntry.Object);

            // Act
         var result = await service.GetRecommendationsForProductAsync(productId);

      // Assert
        Assert.NotNull(result);
       Assert.Single(result);
            _mockRecommendationRepo.Verify(x => x.GetFrequentlyBoughtTogetherAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
     _mockRecommendationRepo.Verify(x => x.GetRelatedProductsByCategoryAsync(productId, categoryId, 6), Times.Once);
        }
    }
}
