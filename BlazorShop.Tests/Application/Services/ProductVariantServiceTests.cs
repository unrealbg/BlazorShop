namespace BlazorShop.Tests.Application.Services
{
    using AutoMapper;

    using BlazorShop.Application.DTOs.Product.ProductVariant;
    using BlazorShop.Application.Services;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;

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
