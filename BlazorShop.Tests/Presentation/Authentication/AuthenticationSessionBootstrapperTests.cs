namespace BlazorShop.Tests.Presentation.Authentication
{
    using BlazorShop.Web.Authentication.Providers;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Helper.Contracts;
    using BlazorShop.Web.Shared.Models;

    using Moq;

    using Xunit;

    public class AuthenticationSessionBootstrapperTests
    {
        [Fact]
        public async Task RestoreAsync_WhenTokenAlreadyExists_DoesNotRefresh()
        {
            var tokenService = new Mock<ITokenService>();
            tokenService
                .Setup(service => service.GetJwtTokenAsync(Constant.TokenStorage.Key))
                .ReturnsAsync("existing-token");

            var refresher = new Mock<IAuthenticationSessionRefresher>();
            var bootstrapper = new AuthenticationSessionBootstrapper(tokenService.Object, refresher.Object);

            await bootstrapper.RestoreAsync();

            refresher.Verify(service => service.TryRefreshAsync(It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task RestoreAsync_WhenTokenIsMissing_AttemptsCookieBackedRefreshWithoutClearing()
        {
            var tokenService = new Mock<ITokenService>();
            tokenService
                .Setup(service => service.GetJwtTokenAsync(Constant.TokenStorage.Key))
                .ReturnsAsync(string.Empty);

            var refresher = new Mock<IAuthenticationSessionRefresher>();
            refresher
                .Setup(service => service.TryRefreshAsync(false))
                .ReturnsAsync((LoginResponse?)null);

            var bootstrapper = new AuthenticationSessionBootstrapper(tokenService.Object, refresher.Object);

            await bootstrapper.RestoreAsync();

            refresher.Verify(service => service.TryRefreshAsync(false), Times.Once);
        }
    }
}