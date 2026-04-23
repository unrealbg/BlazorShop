namespace BlazorShop.Tests.Presentation.Authentication
{
    using System.Net;

    using BlazorShop.Web.Authentication.Providers;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Services.Contracts;

    using Moq;

    using Xunit;

    public class AuthenticationSessionRefresherTests
    {
        [Fact]
        public async Task TryRefreshAsync_WhenRefreshSucceeds_StoresToken_AndNotifiesAuthState()
        {
            const string refreshedToken = "refreshed-token";

            var tokenService = new Mock<ITokenService>();
            tokenService
                .Setup(service => service.StoreJwtTokenAsync(Constant.TokenStorage.Key, refreshedToken))
                .Returns(Task.CompletedTask);

            var authenticationService = new Mock<IAuthenticationService>();
            authenticationService
                .Setup(service => service.ReviveToken())
                .ReturnsAsync(QueryResult<LoginResponse>.Succeeded(new LoginResponse(true, "Token revived successfully.", refreshedToken)));

            var authStateNotifier = new Mock<IAuthenticationStateNotifier>();
            var cleaner = new Mock<IAuthenticatedClientStateCleaner>();
            var publisher = new Mock<IAuthenticationSessionEventPublisher>();
            var refresher = new AuthenticationSessionRefresher(tokenService.Object, authenticationService.Object, authStateNotifier.Object, cleaner.Object, publisher.Object);

            var result = await refresher.TryRefreshAsync();

            Assert.NotNull(result);
            Assert.Equal(refreshedToken, result!.Token);
            tokenService.Verify(service => service.StoreJwtTokenAsync(Constant.TokenStorage.Key, refreshedToken), Times.Once);
            tokenService.Verify(service => service.RemoveJwtTokenAsync(It.IsAny<string>()), Times.Never);
            cleaner.Verify(service => service.ClearAsync(), Times.Never);
            authStateNotifier.Verify(notifier => notifier.NotifyAuthenticationState(), Times.Once);
        }

        [Fact]
        public async Task TryRefreshAsync_WhenRefreshReturnsInvalidSession_RemovesToken_AndNotifiesAuthState()
        {
            var tokenService = new Mock<ITokenService>();
            tokenService
                .Setup(service => service.RemoveJwtTokenAsync(Constant.TokenStorage.Key))
                .Returns(Task.CompletedTask);

            var authenticationService = new Mock<IAuthenticationService>();
            authenticationService
                .Setup(service => service.ReviveToken())
                .ReturnsAsync(QueryResult<LoginResponse>.Failed("Invalid token.", HttpStatusCode.BadRequest));

            var authStateNotifier = new Mock<IAuthenticationStateNotifier>();
            var cleaner = new Mock<IAuthenticatedClientStateCleaner>();
            cleaner.Setup(service => service.ClearAsync()).Returns(Task.CompletedTask);
            var publisher = new Mock<IAuthenticationSessionEventPublisher>();
            publisher.Setup(service => service.PublishSignedOutAsync()).Returns(Task.CompletedTask);
            var refresher = new AuthenticationSessionRefresher(tokenService.Object, authenticationService.Object, authStateNotifier.Object, cleaner.Object, publisher.Object);

            var result = await refresher.TryRefreshAsync();

            Assert.Null(result);
            tokenService.Verify(service => service.StoreJwtTokenAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            tokenService.Verify(service => service.RemoveJwtTokenAsync(Constant.TokenStorage.Key), Times.Once);
            cleaner.Verify(service => service.ClearAsync(), Times.Once);
            authStateNotifier.Verify(notifier => notifier.NotifyAuthenticationState(), Times.Once);
            publisher.Verify(service => service.PublishSignedOutAsync(), Times.Once);
        }

        [Fact]
        public async Task TryRefreshAsync_WhenRefreshFailsWithoutClearing_LeavesTokenUntouched()
        {
            var tokenService = new Mock<ITokenService>();
            var authenticationService = new Mock<IAuthenticationService>();
            authenticationService
                .Setup(service => service.ReviveToken())
                .ReturnsAsync(QueryResult<LoginResponse>.Failed("Invalid token.", HttpStatusCode.BadRequest));

            var authStateNotifier = new Mock<IAuthenticationStateNotifier>();
            var cleaner = new Mock<IAuthenticatedClientStateCleaner>();
            var publisher = new Mock<IAuthenticationSessionEventPublisher>();
            var refresher = new AuthenticationSessionRefresher(tokenService.Object, authenticationService.Object, authStateNotifier.Object, cleaner.Object, publisher.Object);

            var result = await refresher.TryRefreshAsync(clearTokenOnFailure: false);

            Assert.Null(result);
            tokenService.Verify(service => service.StoreJwtTokenAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            tokenService.Verify(service => service.RemoveJwtTokenAsync(It.IsAny<string>()), Times.Never);
            cleaner.Verify(service => service.ClearAsync(), Times.Never);
            authStateNotifier.Verify(notifier => notifier.NotifyAuthenticationState(), Times.Never);
        }

        [Fact]
        public async Task TryRefreshAsync_WhenRefreshFailsDueToServerOutage_DoesNotClearToken()
        {
            var tokenService = new Mock<ITokenService>();
            var authenticationService = new Mock<IAuthenticationService>();
            authenticationService
                .Setup(service => service.ReviveToken())
                .ReturnsAsync(QueryResult<LoginResponse>.Failed("Connection error", HttpStatusCode.ServiceUnavailable));

            var authStateNotifier = new Mock<IAuthenticationStateNotifier>();
            var cleaner = new Mock<IAuthenticatedClientStateCleaner>();
            var publisher = new Mock<IAuthenticationSessionEventPublisher>();
            var refresher = new AuthenticationSessionRefresher(tokenService.Object, authenticationService.Object, authStateNotifier.Object, cleaner.Object, publisher.Object);

            var result = await refresher.TryRefreshAsync();

            Assert.Null(result);
            tokenService.Verify(service => service.RemoveJwtTokenAsync(It.IsAny<string>()), Times.Never);
            cleaner.Verify(service => service.ClearAsync(), Times.Never);
            authStateNotifier.Verify(notifier => notifier.NotifyAuthenticationState(), Times.Never);
            publisher.Verify(service => service.PublishSignedOutAsync(), Times.Never);
        }
    }
}