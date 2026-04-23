namespace BlazorShop.Tests.Presentation.Authentication
{
    using BlazorShop.Web.Authentication.Providers;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;

    using Microsoft.JSInterop;

    using Moq;

    using Xunit;

    public class AuthenticationSessionSyncServiceTests
    {
        [Fact]
        public async Task HandleAuthSessionEventAsync_WhenSignedIn_RefreshesWithoutClearing()
        {
            var refresher = new Mock<IAuthenticationSessionRefresher>();
            refresher
                .Setup(service => service.TryRefreshAsync(false))
                .ReturnsAsync((LoginResponse?)null);

            var tokenService = new Mock<ITokenService>();
            var cleaner = new Mock<IAuthenticatedClientStateCleaner>();
            var notifier = new Mock<IAuthenticationStateNotifier>();
            var jsRuntime = new Mock<IJSRuntime>();

            var service = new AuthenticationSessionSyncService(refresher.Object, tokenService.Object, cleaner.Object, notifier.Object, jsRuntime.Object);

            await service.HandleAuthSessionEventAsync("signed-in");

            refresher.Verify(sync => sync.TryRefreshAsync(false), Times.Once);
            tokenService.Verify(sync => sync.RemoveJwtTokenAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task HandleAuthSessionEventAsync_WhenSignedOut_ClearsSessionStateAndNotifies()
        {
            var refresher = new Mock<IAuthenticationSessionRefresher>();
            var tokenService = new Mock<ITokenService>();
            tokenService
                .Setup(service => service.RemoveJwtTokenAsync(Constant.TokenStorage.Key))
                .Returns(Task.CompletedTask);

            var cleaner = new Mock<IAuthenticatedClientStateCleaner>();
            cleaner.Setup(service => service.ClearAsync()).Returns(Task.CompletedTask);

            var notifier = new Mock<IAuthenticationStateNotifier>();
            var jsRuntime = new Mock<IJSRuntime>();

            var service = new AuthenticationSessionSyncService(refresher.Object, tokenService.Object, cleaner.Object, notifier.Object, jsRuntime.Object);

            await service.HandleAuthSessionEventAsync("signed-out");

            tokenService.Verify(sync => sync.RemoveJwtTokenAsync(Constant.TokenStorage.Key), Times.Once);
            cleaner.Verify(sync => sync.ClearAsync(), Times.Once);
            notifier.Verify(sync => sync.NotifyAuthenticationState(), Times.Once);
            refresher.Verify(sync => sync.TryRefreshAsync(It.IsAny<bool>()), Times.Never);
        }
    }
}