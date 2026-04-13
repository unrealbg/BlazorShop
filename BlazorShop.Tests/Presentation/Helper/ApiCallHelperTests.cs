namespace BlazorShop.Tests.Presentation.Helper
{
    using System.Net;
    using System.Net.Http.Json;

    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Helper;
    using BlazorShop.Web.Shared.Models;

    using Xunit;

    public class ApiCallHelperTests
    {
        [Fact]
        public async Task ApiCallTypeCall_ReturnsServiceUnavailableResponse_WhenGetRequestFails()
        {
            var helper = new ApiCallHelper();
            var client = new HttpClient(new ThrowingHttpMessageHandler())
            {
                BaseAddress = new Uri("https://localhost:7094/api/")
            };

            var apiCall = new ApiCall
            {
                Client = client,
                Route = "product/all",
                Type = Constant.ApiCallType.Get
            };

            var response = await helper.ApiCallTypeCall<Unit>(apiCall);
            var payload = await response.Content.ReadFromJsonAsync<MessagePayload>();

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.NotNull(response.RequestMessage);
            Assert.Equal(new Uri("https://localhost:7094/api/product/all"), response.RequestMessage.RequestUri);
            Assert.NotNull(payload);
            Assert.Equal("Error occurred while connecting to the server", payload!.Message);
        }

        [Fact]
        public async Task GetQueryResult_ReturnsResponseMessage_WhenResponseIsClientError()
        {
            var helper = new ApiCallHelper();
            var response = new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = JsonContent.Create(new { message = "Product not found" })
            };

            var result = await helper.GetQueryResult<object>(response, "Fallback message");

            Assert.False(result.Success);
            Assert.Equal("Product not found", result.Message);
            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        }

        [Fact]
        public async Task GetQueryResult_ReturnsDefaultMessage_WhenResponseIsServerError()
        {
            var helper = new ApiCallHelper();
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = JsonContent.Create(new { message = "Raw server error" })
            };

            var result = await helper.GetQueryResult<object>(response, "We couldn't load products right now. Please try again.");

            Assert.False(result.Success);
            Assert.Equal("We couldn't load products right now. Please try again.", result.Message);
            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        }

        private sealed record MessagePayload(string Message);

        private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw new HttpRequestException("Connection refused");
            }
        }
    }
}