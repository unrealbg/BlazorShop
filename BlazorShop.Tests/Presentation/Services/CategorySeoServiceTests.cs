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

    public class CategorySeoServiceTests
    {
        private readonly CategorySeoService _service;
        private readonly Mock<IHttpClientHelper> _httpClientHelperMock;
        private readonly Mock<IApiCallHelper> _apiCallHelperMock;

        public CategorySeoServiceTests()
        {
            _httpClientHelperMock = new Mock<IHttpClientHelper>();
            _apiCallHelperMock = new Mock<IApiCallHelper>();
            _service = new CategorySeoService(_httpClientHelperMock.Object, _apiCallHelperMock.Object);
        }

        [Fact]
        public async Task GetByCategoryIdAsync_ShouldReturnSeo_WhenApiCallIsSuccessful()
        {
            var client = new HttpClient();
            _httpClientHelperMock.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            _apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var seo = new GetCategorySeo { CategoryId = Guid.NewGuid(), Slug = "trainers" };
            _apiCallHelperMock
                .Setup(helper => helper.GetQueryResult<GetCategorySeo>(apiCallResult, It.IsAny<string>()))
                .ReturnsAsync(QueryResult<GetCategorySeo>.Succeeded(seo));

            var result = await _service.GetByCategoryIdAsync(seo.CategoryId);

            Assert.True(result.Success);
            Assert.Equal("trainers", result.Data?.Slug);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnValidationResponse_WhenApiCallReturnsBadRequest()
        {
            var client = new HttpClient();
            _httpClientHelperMock.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.BadRequest);
            _apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<UpdateCategorySeo>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var response = new ServiceResponse<GetCategorySeo>(Message: "Published categories require a slug")
            {
                ResponseType = ServiceResponseType.ValidationError,
            };

            _apiCallHelperMock
                .Setup(helper => helper.GetMutationResponse<GetCategorySeo>(apiCallResult, It.IsAny<string>()))
                .ReturnsAsync(response);

            var result = await _service.UpdateAsync(Guid.NewGuid(), new UpdateCategorySeo { IsPublished = true });

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.ValidationError, result.ResponseType);
            Assert.Equal("Published categories require a slug", result.Message);
        }
    }
}