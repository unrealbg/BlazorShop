namespace BlazorShop.Tests.Presentation.Services
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Seo;
    using BlazorShop.Web.Shared.Services;

    using Moq;

    using Xunit;

    public class SeoRedirectServiceTests
    {
        private readonly SeoRedirectService _service;
        private readonly Mock<IHttpClientHelper> _httpClientHelperMock;
        private readonly Mock<IApiCallHelper> _apiCallHelperMock;

        public SeoRedirectServiceTests()
        {
            _httpClientHelperMock = new Mock<IHttpClientHelper>();
            _apiCallHelperMock = new Mock<IApiCallHelper>();
            _service = new SeoRedirectService(_httpClientHelperMock.Object, _apiCallHelperMock.Object);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnRedirects_WhenApiCallIsSuccessful()
        {
            var client = new HttpClient();
            _httpClientHelperMock.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.OK);
            _apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<Unit>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            IReadOnlyList<GetSeoRedirect> redirects =
            [
                new GetSeoRedirect { Id = Guid.NewGuid(), OldPath = "/old", NewPath = "/new", StatusCode = 301, IsActive = true },
            ];

            _apiCallHelperMock
                .Setup(helper => helper.GetQueryResult<IReadOnlyList<GetSeoRedirect>>(apiCallResult, It.IsAny<string>()))
                .ReturnsAsync(QueryResult<IReadOnlyList<GetSeoRedirect>>.Succeeded(redirects));

            var result = await _service.GetAllAsync();

            Assert.True(result.Success);
            Assert.Single(result.Data!);
        }

        [Fact]
        public async Task CreateAsync_ShouldReturnCreatedRedirect_WhenApiCallIsSuccessful()
        {
            var client = new HttpClient();
            _httpClientHelperMock.Setup(helper => helper.GetPrivateClientAsync()).ReturnsAsync(client);

            var apiCallResult = new HttpResponseMessage(HttpStatusCode.Created);
            _apiCallHelperMock
                .Setup(helper => helper.ApiCallTypeCall<UpsertSeoRedirect>(It.IsAny<ApiCall>()))
                .ReturnsAsync(apiCallResult);

            var response = new ServiceResponse<GetSeoRedirect>(Success: true, Message: "Created", Id: Guid.NewGuid())
            {
                Payload = new GetSeoRedirect { OldPath = "/sale", NewPath = "/clearance", StatusCode = 301, IsActive = true },
                ResponseType = ServiceResponseType.Success,
            };

            _apiCallHelperMock
                .Setup(helper => helper.GetMutationResponse<GetSeoRedirect>(apiCallResult, It.IsAny<string>()))
                .ReturnsAsync(response);

            var result = await _service.CreateAsync(new UpsertSeoRedirect { OldPath = "/sale", NewPath = "/clearance", StatusCode = 301, IsActive = true });

            Assert.True(result.Success);
            Assert.Equal("Created", result.Message);
            Assert.Equal("/sale", result.Payload?.OldPath);
        }
    }
}