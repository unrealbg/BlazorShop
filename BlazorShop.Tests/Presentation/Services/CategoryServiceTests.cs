namespace BlazorShop.Tests.Presentation.Services
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Services;
    using BlazorShop.Web.Shared.Services.Contracts;

    using Moq;

    using Xunit;

    public class CategoryServiceTests
    {
        private readonly CategoryService _categoryService;
        private readonly Mock<IHttpClientHelper> _httpClientHelperMock;
        private readonly Mock<IApiCallHelper> _apiCallHelperMock;

        public CategoryServiceTests()
        {
            _httpClientHelperMock = new Mock<IHttpClientHelper>();
            _apiCallHelperMock = new Mock<IApiCallHelper>();
            _categoryService = new CategoryService(_httpClientHelperMock.Object, _apiCallHelperMock.Object);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnCategories_WhenApiCallIsSuccessful()
        {
            // Arrange
            var client = new HttpClient();
            _httpClientHelperMock.Setup(helper => helper.GetPublicClient()).Returns(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            _apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var categories = new List<GetCategory>
            {
                new GetCategory
                {
                    Id = Guid.NewGuid(),
                    Name = "Category1",
                    Products = new List<GetProduct>()
                }
            };

            _apiCallHelperMock
                .Setup(helper => helper.GetQueryResult<IEnumerable<GetCategory>>(apiCallResult, It.IsAny<string>()))
                .ReturnsAsync(QueryResult<IEnumerable<GetCategory>>.Succeeded(categories));

            // Act
            var result = await _categoryService.GetAllAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(categories, result.Data);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCategory_WhenApiCallIsSuccessful()
        {
            // Arrange
            var client = new HttpClient();
            _httpClientHelperMock.Setup(helper => helper.GetPublicClient()).Returns(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            _apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var category = new GetCategory
            {
                Id = Guid.NewGuid(),
                Name = "Category1",
                Products = new List<GetProduct>()
            };

            _apiCallHelperMock
                .Setup(helper => helper.GetQueryResult<GetCategory>(apiCallResult, It.IsAny<string>()))
                .ReturnsAsync(QueryResult<GetCategory>.Succeeded(category));

            // Act
            var result = await _categoryService.GetByIdAsync(category.Id);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(category, result.Data);
        }

        [Fact]
        public async Task AddAsync_ShouldReturnServiceResponse_WhenApiCallIsSuccessful()
        {
            // Arrange
            var client = new HttpClient();
            _httpClientHelperMock.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            _apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<CreateCategory>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var serviceResponse = new ServiceResponse { Success = true };
            _apiCallHelperMock
                .Setup(helper => helper.GetServiceResponse<ServiceResponse>(apiCallResult))
                .ReturnsAsync(serviceResponse);

            var category = new CreateCategory
            {
                Name = "New Category"
            };

            // Act
            var result = await _categoryService.AddAsync(category);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnServiceResponse_WhenApiCallIsSuccessful()
        {
            // Arrange
            var client = new HttpClient();
            _httpClientHelperMock.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            _apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<UpdateCategory>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var serviceResponse = new ServiceResponse { Success = true };
            _apiCallHelperMock
                .Setup(helper => helper.GetServiceResponse<ServiceResponse>(apiCallResult))
                .ReturnsAsync(serviceResponse);

            var category = new UpdateCategory
            {
                Id = Guid.NewGuid(),
                Name = "Updated Category"
            };

            // Act
            var result = await _categoryService.UpdateAsync(category);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnServiceResponse_WhenApiCallIsSuccessful()
        {
            // Arrange
            var client = new HttpClient();
            _httpClientHelperMock.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            _apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var serviceResponse = new ServiceResponse { Success = true };
            _apiCallHelperMock
                .Setup(helper => helper.GetServiceResponse<ServiceResponse>(apiCallResult))
                .ReturnsAsync(serviceResponse);

            var categoryId = Guid.NewGuid();

            // Act
            var result = await _categoryService.DeleteAsync(categoryId);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
        }

        [Fact]
        public async Task GetProductsByCategoryAsync_ShouldReturnProducts_WhenApiCallIsSuccessful()
        {
            // Arrange
            var client = new HttpClient();
            _httpClientHelperMock.Setup(helper => helper.GetPublicClient()).Returns(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            _apiCallHelperMock
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
                    CategoryId = Guid.NewGuid(),
                    CreatedOn = DateTime.UtcNow
                }
            };

            _apiCallHelperMock
                .Setup(helper => helper.GetQueryResult<IEnumerable<GetProduct>>(apiCallResult, It.IsAny<string>()))
                .ReturnsAsync(QueryResult<IEnumerable<GetProduct>>.Succeeded(products));

            var categoryId = Guid.NewGuid();

            // Act
            var result = await _categoryService.GetProductsByCategoryAsync(categoryId);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Success);
            Assert.Equal(products, result.Data);
        }
    }
}
