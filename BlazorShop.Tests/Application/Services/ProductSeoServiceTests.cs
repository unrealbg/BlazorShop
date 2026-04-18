namespace BlazorShop.Tests.Application.Services
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services;
    using BlazorShop.Application.Validations;
    using BlazorShop.Application.Validations.Seo;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Tests.TestUtilities;

    using Moq;

    using Xunit;

    public class ProductSeoServiceTests
    {
        private readonly Mock<IGenericRepository<Product>> _productRepository;
        private readonly Mock<IProductReadRepository> _productReadRepository;
        private readonly ProductSeoService _service;

        public ProductSeoServiceTests()
        {
            this._productRepository = new Mock<IGenericRepository<Product>>();
            this._productReadRepository = new Mock<IProductReadRepository>();

            var slugService = new SlugService();
            this._service = new ProductSeoService(
                this._productRepository.Object,
                this._productReadRepository.Object,
                AutoMapperTestFactory.CreateMapper(),
                slugService,
                new ValidationService(),
                new UpdateProductSeoDtoValidator(slugService));
        }

        [Fact]
        public async Task GetByProductIdAsync_WhenProductExists_ReturnsMappedSeoPayload()
        {
            var productId = Guid.NewGuid();
            var product = new Product
            {
                Id = productId,
                Slug = "running-shoes",
                MetaTitle = "Running Shoes",
                RobotsIndex = true,
                RobotsFollow = true,
                IsPublished = true,
            };

            this._productRepository
                .Setup(repository => repository.GetByIdAsync(productId))
                .ReturnsAsync(product);

            var result = await this._service.GetByProductIdAsync(productId);

            Assert.True(result.Success);
            Assert.Equal(ServiceResponseType.Success, result.ResponseType);
            Assert.NotNull(result.Payload);
            Assert.Equal(productId, result.Payload!.ProductId);
            Assert.Equal("running-shoes", result.Payload.Slug);
        }

        [Fact]
        public async Task UpdateAsync_WhenSlugIsDuplicate_ReturnsConflict()
        {
            var productId = Guid.NewGuid();
            var existingProduct = new Product { Id = productId, Slug = "existing-slug", IsPublished = true };

            this._productRepository
                .Setup(repository => repository.GetByIdAsync(productId))
                .ReturnsAsync(existingProduct);
            this._productReadRepository
                .Setup(repository => repository.ProductSlugExistsAsync("summer-sale-2026", productId))
                .ReturnsAsync(true);

            var result = await this._service.UpdateAsync(productId, new UpdateProductSeoDto
            {
                Slug = "Summer Sale 2026",
                IsPublished = true,
            });

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.Conflict, result.ResponseType);
            Assert.Equal("Product slug is already in use.", result.Message);
            this._productRepository.Verify(repository => repository.UpdateAsync(It.IsAny<Product>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenProductDoesNotExist_ReturnsNotFound()
        {
            var productId = Guid.NewGuid();

            this._productRepository
                .Setup(repository => repository.GetByIdAsync(productId))
                .ReturnsAsync((Product?)null);

            var result = await this._service.UpdateAsync(productId, new UpdateProductSeoDto
            {
                Slug = "valid-product-slug",
                IsPublished = true,
            });

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.NotFound, result.ResponseType);
            Assert.Equal("Product not found.", result.Message);
        }

        [Fact]
        public async Task UpdateAsync_WhenPayloadIsValid_NormalizesSlugAndUpdatesEntity()
        {
            var productId = Guid.NewGuid();
            var existingProduct = new Product
            {
                Id = productId,
                Name = "Running Shoes",
                Slug = "old-slug",
                IsPublished = true,
                PublishedOn = null,
            };

            this._productRepository
                .Setup(repository => repository.GetByIdAsync(productId))
                .ReturnsAsync(existingProduct);
            this._productReadRepository
                .Setup(repository => repository.ProductSlugExistsAsync("summer-sale-2026", productId))
                .ReturnsAsync(false);
            this._productRepository
                .Setup(repository => repository.UpdateAsync(existingProduct))
                .ReturnsAsync(1);

            var result = await this._service.UpdateAsync(productId, new UpdateProductSeoDto
            {
                Slug = " Summer Sale 2026 ",
                MetaTitle = "Summer Sale",
                IsPublished = true,
            });

            Assert.True(result.Success);
            Assert.Equal(ServiceResponseType.Success, result.ResponseType);
            Assert.Equal("summer-sale-2026", existingProduct.Slug);
            Assert.Equal("Summer Sale", existingProduct.MetaTitle);
            Assert.NotNull(existingProduct.PublishedOn);
            this._productRepository.Verify(repository => repository.UpdateAsync(existingProduct), Times.Once);
        }
    }
}