namespace BlazorShop.Tests.Presentation.Services
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Seo;
    using BlazorShop.Web.Shared.Services;

    using Moq;

    using Xunit;

    public class ProductSeoServiceTests
    {
        private readonly ProductSeoService _service;
        private readonly Mock<IHttpClientHelper> _httpClientHelperMock;
        private readonly Mock<IApiCallHelper> _apiCallHelperMock;

        public ProductSeoServiceTests()
        {
            _httpClientHelperMock = new Mock<IHttpClientHelper>();
            _apiCallHelperMock = new Mock<IApiCallHelper>();
            _service = new ProductSeoService(_httpClientHelperMock.Object, _apiCallHelperMock.Object);
        }

        [Fact]
        public async Task GetByProductIdAsync_ShouldReturnSeo_WhenApiCallIsSuccessful()
        {
            var client = new HttpClient();
            _httpClientHelperMock.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            _apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var seo = new GetProductSeo { ProductId = Guid.NewGuid(), Slug = "running-shoe" };
            _apiCallHelperMock
                .Setup(helper => helper.GetQueryResult<GetProductSeo>(apiCallResult, It.IsAny<string>()))
                .ReturnsAsync(QueryResult<GetProductSeo>.Succeeded(seo));

            var result = await _service.GetByProductIdAsync(seo.ProductId);

            Assert.True(result.Success);
            Assert.Equal("running-shoe", result.Data?.Slug);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnConflictResponse_WhenApiCallReturnsConflict()
        {
            var client = new HttpClient();
            _httpClientHelperMock.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.Conflict);
            _apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<UpdateProductSeo>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var response = new ServiceResponse<GetProductSeo>(Message: "Slug already exists")
            {
                ResponseType = ServiceResponseType.Conflict,
            };

            _apiCallHelperMock
                .Setup(helper => helper.GetMutationResponse<GetProductSeo>(apiCallResult, It.IsAny<string>()))
                .ReturnsAsync(response);

            var result = await _service.UpdateAsync(Guid.NewGuid(), new UpdateProductSeo { Slug = "conflicting-slug" });

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.Conflict, result.ResponseType);
            Assert.Equal("Slug already exists", result.Message);
        }
    }
}