namespace BlazorShop.Tests.Application.Services
{
    using AutoMapper;

    using BlazorShop.Application.DTOs.Product.ProductVariant;
    using BlazorShop.Application.Services;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Tests.TestUtilities;

    using Moq;

    using Xunit;

    public sealed class ProductVariantServiceTests
    {
        [Fact]
        public async Task UpdateAsync_DoesNotMutateStock()
        {
            var variantId = Guid.NewGuid();
            var request = new UpdateProductVariant { Id = variantId, ProductId = Guid.NewGuid(), Stock = 4 };
            var existing = new ProductVariant { Id = variantId, ProductId = request.ProductId, Stock = 1 };
            var repository = new Mock<IGenericRepository<ProductVariant>>();
            repository.Setup(item => item.GetByIdAsync(variantId)).ReturnsAsync(existing);
            repository.Setup(item => item.UpdateAsync(existing)).ReturnsAsync(1);
            var mapper = new Mock<IMapper>();
            mapper.Setup(item => item.Map(request, existing))
                .Callback<UpdateProductVariant, ProductVariant>((source, destination) => destination.Stock = source.Stock)
                .Returns(existing);
            var topologyRepository = new Mock<IProductInventoryTopologyRepository>();
            var service = new ProductVariantService(repository.Object, topologyRepository.Object, mapper.Object);

            var result = await service.UpdateAsync(request);

            Assert.True(result.Success);
            Assert.Equal(1, existing.Stock);
            repository.Verify(item => item.UpdateAsync(existing), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_DoesNotAllowChangingProductId()
        {
            var variantId = Guid.NewGuid();
            var originalProductId = Guid.NewGuid();
            var existing = new ProductVariant
            {
                Id = variantId,
                ProductId = originalProductId,
                Stock = 3,
            };
            var request = new UpdateProductVariant
            {
                Id = variantId,
                ProductId = Guid.NewGuid(),
                Stock = 9,
            };
            var repository = new Mock<IGenericRepository<ProductVariant>>();
            repository.Setup(item => item.GetByIdAsync(variantId)).ReturnsAsync(existing);
            var mapper = new Mock<IMapper>();
            var topologyRepository = new Mock<IProductInventoryTopologyRepository>();
            var service = new ProductVariantService(repository.Object, topologyRepository.Object, mapper.Object);

            var result = await service.UpdateAsync(request);

            Assert.False(result.Success);
            Assert.Equal("A product variant cannot be moved to another product", result.Message);
            Assert.Equal(originalProductId, existing.ProductId);
            Assert.Equal(3, existing.Stock);
            mapper.Verify(item => item.Map(request, existing), Times.Never);
            repository.Verify(item => item.UpdateAsync(It.IsAny<ProductVariant>()), Times.Never);
        }

        [Fact]
        public void UpdateMapping_DoesNotMutateStockOrProductId()
        {
            var originalProductId = Guid.NewGuid();
            var destination = new ProductVariant
            {
                Id = Guid.NewGuid(),
                ProductId = originalProductId,
                Stock = 3,
            };
            var source = new UpdateProductVariant
            {
                Id = destination.Id,
                ProductId = Guid.NewGuid(),
                Stock = 9,
            };

            AutoMapperTestFactory.CreateMapper().Map(source, destination);

            Assert.Equal(originalProductId, destination.ProductId);
            Assert.Equal(3, destination.Stock);
        }

        [Fact]
        public async Task AddAsync_CreatesVariantWithZeroStock()
        {
            var request = new CreateProductVariant { ProductId = Guid.NewGuid(), Stock = 9 };
            var mapped = new ProductVariant { ProductId = request.ProductId, Stock = 9 };
            var repository = new Mock<IGenericRepository<ProductVariant>>();
            var mapper = new Mock<IMapper>();
            mapper.Setup(item => item.Map<ProductVariant>(request)).Returns(mapped);
            var topologyRepository = new Mock<IProductInventoryTopologyRepository>();
            topologyRepository
                .Setup(item => item.AddVariantAsync(mapped, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProductInventoryTopologyResult(true, "Variant added successfully"));
            var service = new ProductVariantService(repository.Object, topologyRepository.Object, mapper.Object);

            var result = await service.AddAsync(request);

            Assert.True(result.Success);
            Assert.NotEqual(Guid.Empty, mapped.Id);
            Assert.Equal(0, mapped.Stock);
            topologyRepository.Verify(
                item => item.AddVariantAsync(mapped, It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
