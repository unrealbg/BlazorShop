namespace BlazorShop.Tests.Presentation.Authentication
{
    using System.Net;
    using System.Net.Http.Headers;

    using BlazorShop.Web.Authentication.Providers;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models;

    using Moq;

    using Xunit;

    public class RefreshTokenHandlerTests
    {
        [Fact]
        public async Task SendAsync_WhenRefreshSucceeds_RetriesRequestWithNewToken_AndNotifiesAuthState()
        {
            const string refreshedToken = "refreshed-token";

            var sessionRefresher = new Mock<IAuthenticationSessionRefresher>();
            sessionRefresher
                .Setup(service => service.TryRefreshAsync(true))
                .ReturnsAsync(new LoginResponse(true, "Token revived successfully.", refreshedToken));
            var innerHandler = new RecordingHandler(HttpStatusCode.Unauthorized, HttpStatusCode.OK);

            using var client = CreateClient(sessionRefresher.Object, innerHandler);

            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/api/orders");
            request.Headers.Authorization = new AuthenticationHeaderValue(Constant.Authentication.Type, "expired-token");

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, innerHandler.CallCount);
            Assert.Equal("Bearer expired-token", innerHandler.AuthorizationHeaders[0]);
            Assert.Equal($"Bearer {refreshedToken}", innerHandler.AuthorizationHeaders[1]);
            sessionRefresher.Verify(service => service.TryRefreshAsync(true), Times.Once);
        }

        [Fact]
        public async Task SendAsync_WhenRefreshFails_ClearsToken_AndNotifiesAuthStateWithoutRetry()
        {
            var sessionRefresher = new Mock<IAuthenticationSessionRefresher>();
            sessionRefresher
                .Setup(service => service.TryRefreshAsync(true))
                .ReturnsAsync((LoginResponse?)null);
            var innerHandler = new RecordingHandler(HttpStatusCode.Unauthorized);

            using var client = CreateClient(sessionRefresher.Object, innerHandler);

            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/api/orders");
            request.Headers.Authorization = new AuthenticationHeaderValue(Constant.Authentication.Type, "expired-token");

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(1, innerHandler.CallCount);
            sessionRefresher.Verify(service => service.TryRefreshAsync(true), Times.Once);
        }

        private static HttpClient CreateClient(
            IAuthenticationSessionRefresher sessionRefresher,
            RecordingHandler innerHandler)
        {
            var refreshHandler = new RefreshTokenHandler(sessionRefresher)
            {
                InnerHandler = innerHandler,
            };

            return new HttpClient(refreshHandler);
        }

        private sealed class RecordingHandler : HttpMessageHandler
        {
            private readonly Queue<HttpStatusCode> _responses;

            public RecordingHandler(params HttpStatusCode[] responses)
            {
                _responses = new Queue<HttpStatusCode>(responses);
            }

            public int CallCount { get; private set; }

            public List<string?> AuthorizationHeaders { get; } = [];

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CallCount++;
                AuthorizationHeaders.Add(request.Headers.Authorization?.ToString());

                var statusCode = _responses.Count > 0
                    ? _responses.Dequeue()
                    : HttpStatusCode.OK;

                return Task.FromResult(new HttpResponseMessage(statusCode)
                {
                    RequestMessage = request,
                });
            }
        }
    }
}