namespace BlazorShop.Tests.Presentation.Services
{
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Seo;
    using BlazorShop.Web.Shared.Services;

    using Moq;

    using Xunit;

    public class SeoSettingsServiceTests
    {
        private readonly SeoSettingsService _service;
        private readonly Mock<IHttpClientHelper> _httpClientHelperMock;
        private readonly Mock<IApiCallHelper> _apiCallHelperMock;

        public SeoSettingsServiceTests()
        {
            _httpClientHelperMock = new Mock<IHttpClientHelper>();
            _apiCallHelperMock = new Mock<IApiCallHelper>();
            _service = new SeoSettingsService(_httpClientHelperMock.Object, _apiCallHelperMock.Object);
        }

        [Fact]
        public async Task GetAsync_ShouldReturnSettings_WhenApiCallIsSuccessful()
        {
            var client = new HttpClient();
            _httpClientHelperMock.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            _apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var settings = new GetSeoSettings { SiteName = "BlazorShop" };
            _apiCallHelperMock
                .Setup(helper => helper.GetQueryResult<GetSeoSettings>(apiCallResult, It.IsAny<string>()))
                .ReturnsAsync(QueryResult<GetSeoSettings>.Succeeded(settings));

            var result = await _service.GetAsync();

            Assert.True(result.Success);
            Assert.Equal("BlazorShop", result.Data?.SiteName);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnUpdatedSettings_WhenApiCallIsSuccessful()
        {
            var client = new HttpClient();
            _httpClientHelperMock.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            _apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<UpdateSeoSettings>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var response = new ServiceResponse<GetSeoSettings>(Success: true, Message: "Saved")
            {
                Payload = new GetSeoSettings { SiteName = "BlazorShop" },
                ResponseType = ServiceResponseType.Success,
            };

            _apiCallHelperMock
                .Setup(helper => helper.GetMutationResponse<GetSeoSettings>(apiCallResult, It.IsAny<string>()))
                .ReturnsAsync(response);

            var result = await _service.UpdateAsync(new UpdateSeoSettings { SiteName = "BlazorShop" });

            Assert.True(result.Success);
            Assert.Equal(ServiceResponseType.Success, result.ResponseType);
            Assert.Equal("BlazorShop", result.Payload?.SiteName);
        }
    }
}