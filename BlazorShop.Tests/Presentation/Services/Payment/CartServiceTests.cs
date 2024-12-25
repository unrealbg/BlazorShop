namespace BlazorShop.Tests.Presentation.Services.Payment
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Services;

    using Moq;

    using Xunit;

    public class CartServiceTests
    {
        private readonly Mock<IHttpClientHelper> _httpClientHelperMock;
        private readonly Mock<IApiCallHelper> _apiCallHelperMock;
        private readonly CartService _cartService;

        public CartServiceTests()
        {
            this._httpClientHelperMock = new Mock<IHttpClientHelper>();
            this._apiCallHelperMock = new Mock<IApiCallHelper>();
            this._cartService = new CartService(this._httpClientHelperMock.Object, this._apiCallHelperMock.Object);
        }

        [Fact]
        public async Task Checkout_Returns_ServiceResponse()
        {
            // Arrange
            var checkout = new Checkout
            {
                PaymentMethodId = Guid.NewGuid(),
                Carts = new List<ProcessCart>
                {
                    new ProcessCart { ProductId = Guid.NewGuid(), Quantity = 1 }
                }
            };

            var httpResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            this._httpClientHelperMock
                .Setup(x => x.GetPrivateClientAsync())
                .ReturnsAsync(new HttpClient());
            this._apiCallHelperMock
                .Setup(x => x.ApiCallTypeCall<Checkout>(It.IsAny<ApiCall>()))
                .ReturnsAsync(httpResponse);
            this._apiCallHelperMock
                .Setup(x => x.GetServiceResponse<ServiceResponse>(httpResponse))
                .ReturnsAsync(new ServiceResponse());

            // Act
            var result = await this._cartService.Checkout(checkout);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task SaveCheckoutHistory_Returns_ServiceResponse()
        {
            // Arrange
            var orderItems = new List<CreateOrderItem>
            {
                new CreateOrderItem { ProductId = Guid.NewGuid(), Quantity = 2, UserId = "user123" }
            };

            var httpResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            this._httpClientHelperMock
                .Setup(x => x.GetPrivateClientAsync())
                .ReturnsAsync(new HttpClient());
            this._apiCallHelperMock
                .Setup(x => x.ApiCallTypeCall<IEnumerable<CreateOrderItem>>(It.IsAny<ApiCall>()))
                .ReturnsAsync(httpResponse);
            this._apiCallHelperMock
                .Setup(x => x.GetServiceResponse<ServiceResponse>(httpResponse))
                .ReturnsAsync(new ServiceResponse());

            // Act
            var result = await this._cartService.SaveCheckoutHistory(orderItems);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetOrderItemsAsync_Returns_OrderItems()
        {
            // Arrange
            var httpResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            var orderItems = new List<GetOrderItem>
            {
                new GetOrderItem
                {
                    ProductName = "Product A",
                    QuantityOrdered = 1,
                    CustomerName = "John Doe",
                    CustomerEmail = "john.doe@example.com",
                    AmountPayed = 99.99m,
                    DatePurchased = DateTime.UtcNow
                }
            };

            this._httpClientHelperMock
                .Setup(x => x.GetPrivateClientAsync())
                .ReturnsAsync(new HttpClient());
            this._apiCallHelperMock
                .Setup(x => x.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>()))
                .ReturnsAsync(httpResponse);
            this._apiCallHelperMock
                .Setup(x => x.GetServiceResponse<IEnumerable<GetOrderItem>>(httpResponse))
                .ReturnsAsync(orderItems);

            // Act
            var result = await this._cartService.GetOrderItemsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }
    }
}
