namespace BlazorShop.Tests.Presentation.Services
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Services;

    using Moq;

    using Xunit;

    public class ProductServiceTests
    {
        private readonly ProductService _productService;
        private readonly Mock<IHttpClientHelper> _httpClientHelperMock;
        private readonly Mock<IApiCallHelper> _apiCallHelperMock;

        public ProductServiceTests()
        {
            this._httpClientHelperMock = new Mock<IHttpClientHelper>();
            this._apiCallHelperMock = new Mock<IApiCallHelper>();
            this._productService = new ProductService(this._httpClientHelperMock.Object, this._apiCallHelperMock.Object);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnProducts_WhenApiCallIsSuccessful()
        {
            // Arrange
            var client = new HttpClient();
            this._httpClientHelperMock.Setup(helper => helper.GetPublicClient()).Returns(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            this._apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var products = new List<GetProduct>
            {
                new GetProduct
                {
                    Id = Guid.NewGuid(),
                    Name = "Product1",
                    Description = "Description1",
                    Price = 10.0m,
                    Quantity = 5,
                    CategoryId = Guid.NewGuid()
                }
            };

            this._apiCallHelperMock
                .Setup(helper => helper.GetServiceResponse<IEnumerable<GetProduct>>(apiCallResult))
                .ReturnsAsync(products);

            // Act
            var result = await this._productService.GetAllAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(products, result);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnProduct_WhenApiCallIsSuccessful()
        {
            // Arrange
            var client = new HttpClient();
            this._httpClientHelperMock.Setup(helper => helper.GetPublicClient()).Returns(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            this._apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var product = new GetProduct
            {
                Id = Guid.NewGuid(),
                Name = "Product1",
                Description = "Description1",
                Price = 10.0m,
                Quantity = 5,
                CategoryId = Guid.NewGuid()
            };

            this._apiCallHelperMock
                .Setup(helper => helper.GetServiceResponse<GetProduct>(apiCallResult))
                .ReturnsAsync(product);

            // Act
            var result = await this._productService.GetByIdAsync(product.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(product, result);
        }

        [Fact]
        public async Task AddAsync_ShouldReturnServiceResponse_WhenApiCallIsSuccessful()
        {
            // Arrange
            var client = new HttpClient();
            this._httpClientHelperMock.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            this._apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<CreateProduct>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var serviceResponse = new ServiceResponse { Success = true };
            this._apiCallHelperMock
                .Setup(helper => helper.GetServiceResponse<ServiceResponse>(apiCallResult))
                .ReturnsAsync(serviceResponse);

            var product = new CreateProduct
            {
                Name = "Product1",
                Description = "Description1",
                Price = 10.0m,
                Quantity = 5,
                CategoryId = Guid.NewGuid(),
                Image = "image.png"
            };

            // Act
            var result = await this._productService.AddAsync(product);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnServiceResponse_WhenApiCallIsSuccessful()
        {
            // Arrange
            var client = new HttpClient();
            this._httpClientHelperMock.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            this._apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<UpdateProduct>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var serviceResponse = new ServiceResponse { Success = true };
            this._apiCallHelperMock
                .Setup(helper => helper.GetServiceResponse<ServiceResponse>(apiCallResult))
                .ReturnsAsync(serviceResponse);

            var product = new UpdateProduct
            {
                Id = Guid.NewGuid(),
                Name = "Product1",
                Description = "Description1",
                Price = 15.0m,
                Quantity = 10,
                CategoryId = Guid.NewGuid(),
                Image = "image_updated.png"
            };

            // Act
            var result = await this._productService.UpdateAsync(product);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnServiceResponse_WhenApiCallIsSuccessful()
        {
            // Arrange
            var client = new HttpClient();
            this._httpClientHelperMock.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            this._apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var serviceResponse = new ServiceResponse { Success = true };
            this._apiCallHelperMock
                .Setup(helper => helper.GetServiceResponse<ServiceResponse>(apiCallResult))
                .ReturnsAsync(serviceResponse);

            var productId = Guid.NewGuid();

            // Act
            var result = await this._productService.DeleteAsync(productId);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
        }
    }
}
