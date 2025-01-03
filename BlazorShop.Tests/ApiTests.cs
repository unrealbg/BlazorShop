namespace BlazorShop.Tests
{
    using System.Net;
    using System.Threading.Tasks;

    using Xunit;

    public class ApiTests : ApiTestBase
    {
        [Fact]
        public async Task GetProductsEndpointReturnsOkStatusCode()
        {
            // Act
            var response = await this.HttpClient.GetAsync("/api/product/all");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetAllProducts_ReturnsOkWithProducts()
        {
            // Act
            var response = await this.HttpClient.GetAsync("/api/product/all");
            var content = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("\"name\":", content);
        }

        [Fact]
        public async Task GetSingleProduct_ReturnsNotFound()
        {
            // Arrange
            var productId = Guid.NewGuid();

            // Act
            var response = await this.HttpClient.GetAsync($"/api/product/single/{productId}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}