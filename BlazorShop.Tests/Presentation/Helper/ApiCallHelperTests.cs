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
        [Theory]
        [InlineData("empty")]
        [InlineData("content")]
        [InlineData("json")]
        public async Task PostCancellation_StopsOneAttemptWithoutRetry(string body)
        {
            var handler = new CancellablePost();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/api/") };
            using var cancellation = new CancellationTokenSource();
            using var content = new StringContent("synthetic");
            var call = new ApiCall
            {
                Client = client, Route = "authentication/refresh-token", Type = Constant.ApiCallType.Post,
                CancellationToken = cancellation.Token,
                Model = body == "empty" ? null : body == "content" ? content : new Dictionary<string, string> { ["value"] = "synthetic" },
            };
            var pending = new ApiCallHelper().ApiCallTypeCall<Dictionary<string, string>>(call);
            await handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();
            using var response = await pending;
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal(1, handler.Attempts);
        }

        private sealed class CancellablePost : HttpMessageHandler
        {
            public int Attempts { get; private set; }
            public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Attempts++;
                var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var registration = cancellationToken.Register(() => pending.TrySetCanceled(cancellationToken));
                Started.TrySetResult();
                return await pending.Task;
            }
        }

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

        [Fact]
        public async Task ApiCallTypeCall_SendsPostWithoutBody_WhenModelIsNull()
        {
            var helper = new ApiCallHelper();
            var handler = new RecordingHttpMessageHandler();
            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri("https://localhost:7094/api/")
            };

            var apiCall = new ApiCall
            {
                Client = client,
                Route = "authentication/refresh-token",
                Type = Constant.ApiCallType.Post
            };

            var response = await helper.ApiCallTypeCall<Unit>(apiCall);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(handler.Request);
            Assert.Equal(HttpMethod.Post, handler.Request!.Method);
            Assert.Equal(new Uri("https://localhost:7094/api/authentication/refresh-token"), handler.Request.RequestUri);
            Assert.Null(handler.Request.Content);
        }

        private sealed record MessagePayload(string Message);

        private sealed class RecordingHttpMessageHandler : HttpMessageHandler
        {
            public HttpRequestMessage? Request { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Request = request;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }
        }

        private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                throw new HttpRequestException("Connection refused");
            }
        }
    }
}
