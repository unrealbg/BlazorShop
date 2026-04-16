namespace BlazorShop.Tests.Presentation.Authentication
{
    using System.Net;
    using System.Net.Http.Headers;

    using BlazorShop.Web.Authentication.Providers;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Services.Contracts;

    using Moq;

    using Xunit;

    public class RefreshTokenHandlerTests
    {
        [Fact]
        public async Task SendAsync_WhenRefreshSucceeds_RetriesRequestWithNewToken_AndNotifiesAuthState()
        {
            const string refreshedToken = "refreshed-token";

            var tokenService = new Mock<ITokenService>();
            tokenService
                .Setup(service => service.StoreJwtTokenAsync(Constant.TokenStorage.Key, refreshedToken))
                .Returns(Task.CompletedTask);

            var authenticationService = new Mock<IAuthenticationService>();
            authenticationService
                .Setup(service => service.ReviveToken())
                .ReturnsAsync(new LoginResponse(true, "Token revived successfully.", refreshedToken));

            var authStateNotifier = new Mock<IAuthenticationStateNotifier>();
            var innerHandler = new RecordingHandler(HttpStatusCode.Unauthorized, HttpStatusCode.OK);

            using var client = CreateClient(tokenService.Object, authenticationService.Object, authStateNotifier.Object, innerHandler);

            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/api/orders");
            request.Headers.Authorization = new AuthenticationHeaderValue(Constant.Authentication.Type, "expired-token");

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, innerHandler.CallCount);
            Assert.Equal("Bearer expired-token", innerHandler.AuthorizationHeaders[0]);
            Assert.Equal($"Bearer {refreshedToken}", innerHandler.AuthorizationHeaders[1]);
            tokenService.Verify(service => service.StoreJwtTokenAsync(Constant.TokenStorage.Key, refreshedToken), Times.Once);
            tokenService.Verify(service => service.RemoveJwtTokenAsync(It.IsAny<string>()), Times.Never);
            authStateNotifier.Verify(notifier => notifier.NotifyAuthenticationState(), Times.Once);
        }

        [Fact]
        public async Task SendAsync_WhenRefreshFails_ClearsToken_AndNotifiesAuthStateWithoutRetry()
        {
            var tokenService = new Mock<ITokenService>();
            tokenService
                .Setup(service => service.RemoveJwtTokenAsync(Constant.TokenStorage.Key))
                .Returns(Task.CompletedTask);

            var authenticationService = new Mock<IAuthenticationService>();
            authenticationService
                .Setup(service => service.ReviveToken())
                .ReturnsAsync(new LoginResponse(Message: "Invalid token."));

            var authStateNotifier = new Mock<IAuthenticationStateNotifier>();
            var innerHandler = new RecordingHandler(HttpStatusCode.Unauthorized);

            using var client = CreateClient(tokenService.Object, authenticationService.Object, authStateNotifier.Object, innerHandler);

            var request = new HttpRequestMessage(HttpMethod.Get, "https://example.test/api/orders");
            request.Headers.Authorization = new AuthenticationHeaderValue(Constant.Authentication.Type, "expired-token");

            var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(1, innerHandler.CallCount);
            tokenService.Verify(service => service.StoreJwtTokenAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            tokenService.Verify(service => service.RemoveJwtTokenAsync(Constant.TokenStorage.Key), Times.Once);
            authStateNotifier.Verify(notifier => notifier.NotifyAuthenticationState(), Times.Once);
        }

        private static HttpClient CreateClient(
            ITokenService tokenService,
            IAuthenticationService authenticationService,
            IAuthenticationStateNotifier authStateNotifier,
            RecordingHandler innerHandler)
        {
            var refreshHandler = new RefreshTokenHandler(tokenService, authenticationService, authStateNotifier)
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