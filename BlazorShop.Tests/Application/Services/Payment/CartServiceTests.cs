namespace BlazorShop.Tests.Application.Services.Payment
{
    using AutoMapper;

    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Payment;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;

    using Moq;

    using Xunit;

    public sealed class CartServiceTests
    {
        [Fact]
        public async Task SaveCheckoutHistoryAsync_UsesAuthenticatedUserInsteadOfPayloadUser()
        {
            var cart = new Mock<ICart>();
            var mapper = new Mock<IMapper>();
            var mappedItems = new List<OrderItem>();
            List<CreateOrderItem>? capturedItems = null;
            mapper.Setup(item => item.Map<IEnumerable<OrderItem>>(It.IsAny<object>()))
                .Callback<object>(items => capturedItems = ((IEnumerable<CreateOrderItem>)items).ToList())
                .Returns(mappedItems);
            cart.Setup(item => item.SaveCheckoutHistory(mappedItems)).ReturnsAsync(1);
            var service = CreateService(cart.Object, mapper.Object);

            var result = await service.SaveCheckoutHistoryAsync(
                "authenticated-user",
                [new CreateOrderItem { ProductId = Guid.NewGuid(), Quantity = 2, UserId = "spoofed-user" }]);

            Assert.True(result.Success);
            Assert.NotNull(capturedItems);
            Assert.All(capturedItems!, item => Assert.Equal("authenticated-user", item.UserId));
        }

        [Fact]
        public async Task SaveCheckoutHistoryAsync_RejectsMissingAuthenticatedUser()
        {
            var cart = new Mock<ICart>();
            var service = CreateService(cart.Object, Mock.Of<IMapper>());

            var result = await service.SaveCheckoutHistoryAsync(
                string.Empty,
                [new CreateOrderItem { ProductId = Guid.NewGuid(), Quantity = 1 }]);

            Assert.False(result.Success);
            cart.Verify(item => item.SaveCheckoutHistory(It.IsAny<IEnumerable<OrderItem>>()), Times.Never);
        }

        [Fact]
        public async Task SaveCheckoutHistoryAsync_RejectsEmptySanitizedHistory()
        {
            var cart = new Mock<ICart>();
            var service = CreateService(cart.Object, Mock.Of<IMapper>());

            var result = await service.SaveCheckoutHistoryAsync(
                "authenticated-user",
                [new CreateOrderItem { ProductId = Guid.Empty, Quantity = 0 }]);

            Assert.False(result.Success);
            cart.Verify(item => item.SaveCheckoutHistory(It.IsAny<IEnumerable<OrderItem>>()), Times.Never);
        }

        private static CartService CreateService(ICart cart, IMapper mapper)
        {
            return new CartService(
                cart,
                mapper,
                Mock.Of<IProductReadRepository>(),
                Mock.Of<IAppUserManager>());
        }
    }
}
