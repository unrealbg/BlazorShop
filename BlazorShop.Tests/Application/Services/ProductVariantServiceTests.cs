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
        public async Task UpdateAsync_UpdatesTheTrackedVariantForStockConcurrency()
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
            var service = new ProductVariantService(repository.Object, mapper.Object);

            var result = await service.UpdateAsync(request);

            Assert.True(result.Success);
            Assert.Equal(4, existing.Stock);
            repository.Verify(item => item.UpdateAsync(existing), Times.Once);
        }
    }
}
