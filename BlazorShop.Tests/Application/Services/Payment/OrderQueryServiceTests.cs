namespace BlazorShop.Tests.Application.Services.Payment
{
    using BlazorShop.Application.Services.Payment;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Domain.Entities.Payment;

    using Moq;

    using Xunit;

    public class OrderQueryServiceTests
    {
        [Fact]
        public async Task GetOrdersForUserAsync_UsesPersistedLineSnapshotsWithoutCatalogData()
        {
            var userId = "user-1";
            var variantId = Guid.NewGuid();
            var orderRepository = new Mock<IOrderRepository>();
            orderRepository
                .Setup(repository => repository.GetByUserIdAsync(userId))
                .ReturnsAsync(
                [
                    new Order
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Reference = "BS-89",
                        TotalAmount = 215m,
                        SubtotalAmount = 215m,
                        Currency = "EUR",
                        CustomerNameSnapshot = "Original Customer",
                        CustomerEmailSnapshot = "original@example.com",
                        Lines =
                        [
                            new OrderLine
                            {
                                ProductId = Guid.NewGuid(),
                                ProductVariantId = variantId,
                                ProductNameSnapshot = "Original Runner",
                                SkuSnapshot = "RUN-42-BLK",
                                SizeScaleSnapshot = "ShoesUS",
                                SizeValueSnapshot = "10",
                                ColorSnapshot = "Black",
                                Quantity = 2,
                                UnitPrice = 95m,
                                LineTotal = 190m,
                            },
                            new OrderLine
                            {
                                ProductId = Guid.NewGuid(),
                                ProductNameSnapshot = "Classic Cap",
                                Quantity = 1,
                                UnitPrice = 25m,
                                LineTotal = 25m,
                            },
                        ],
                    },
                ]);
            var userManager = new Mock<IAppUserManager>();
            userManager
                .Setup(manager => manager.GetUserByIdAsync(userId))
                .ReturnsAsync(new AppUser { Id = userId, UserName = "customer" });
            var service = new OrderQueryService(orderRepository.Object, userManager.Object);

            var result = Assert.Single(await service.GetOrdersForUserAsync(userId));

            Assert.Equal("Original Customer", result.CustomerName);
            Assert.Equal("original@example.com", result.CustomerEmail);
            userManager.Verify(manager => manager.GetUserByIdAsync(It.IsAny<string>()), Times.Never);

            var variantLine = Assert.Single(result.Lines, line => line.VariantId == variantId);
            Assert.Equal("Original Runner", variantLine.ProductName);
            Assert.Equal("RUN-42-BLK", variantLine.Sku);
            Assert.Equal("ShoesUS", variantLine.SizeScale);
            Assert.Equal("10", variantLine.SizeValue);
            Assert.Equal("Black", variantLine.Color);
            Assert.Equal(95m, variantLine.UnitPrice);
            Assert.Equal(190m, variantLine.LineTotal);

            var productOnlyLine = Assert.Single(result.Lines, line => line.VariantId is null);
            Assert.Equal("Classic Cap", productOnlyLine.ProductName);
            Assert.Equal(25m, productOnlyLine.UnitPrice);
            Assert.Equal(25m, productOnlyLine.LineTotal);
        }
    }
}
