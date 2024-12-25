namespace BlazorShop.Tests.Presentation.Services.Payment
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Services;

    using Moq;

    using Xunit;

    public class PaymentMethodServiceTests
    {
        private readonly Mock<IHttpClientHelper> _httpClientHelperMock;
        private readonly Mock<IApiCallHelper> _apiCallHelperMock;
        private readonly PaymentMethodService _paymentMethodService;

        public PaymentMethodServiceTests()
        {
            this._httpClientHelperMock = new Mock<IHttpClientHelper>();
            this._apiCallHelperMock = new Mock<IApiCallHelper>();
            this._paymentMethodService = new PaymentMethodService(
                this._httpClientHelperMock.Object, 
                this._apiCallHelperMock.Object
            );
        }

        [Fact]
        public async Task GetPaymentMethods_ReturnsPaymentMethods_WhenApiCallIsSuccessful()
        {
            // Arrange
            var expectedPaymentMethods = new List<GetPaymentMethod>
            {
                new GetPaymentMethod { Id = Guid.NewGuid(), Name = "Credit Card" },
                new GetPaymentMethod { Id = Guid.NewGuid(), Name = "PayPal" }
            };
            var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK);
            var httpClient = new HttpClient();

            this._httpClientHelperMock
                .Setup(h => h.GetPublicClient())
                .Returns(httpClient);

            this._apiCallHelperMock
                .Setup(a => a.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>()))
                .ReturnsAsync(httpResponseMessage);

            this._apiCallHelperMock
                .Setup(a => a.GetServiceResponse<IEnumerable<GetPaymentMethod>>(httpResponseMessage))
                .ReturnsAsync(expectedPaymentMethods);

            // Act
            var result = await this._paymentMethodService.GetPaymentMethods();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedPaymentMethods, result);
        }

        [Fact]
        public async Task GetPaymentMethods_ReturnsEmptyList_WhenApiCallIsUnsuccessful()
        {
            // Arrange
            var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.BadRequest);
            var httpClient = new HttpClient();

            this._httpClientHelperMock
                .Setup(h => h.GetPublicClient())
                .Returns(httpClient);

            this._apiCallHelperMock
                .Setup(a => a.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>()))
                .ReturnsAsync(httpResponseMessage);

            // Act
            var result = await this._paymentMethodService.GetPaymentMethods();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }
    }
}
