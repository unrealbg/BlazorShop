namespace BlazorShop.Tests.Presentation.Authentication;

using BlazorShop.Web.Authentication.Providers;
using Moq;
using Xunit;

public sealed class AuthenticationSessionBootstrapperTests
{
    [Fact]
    public async Task RestoreAlwaysConsultsCoordinatorWithOnlyIfNeededSemantics()
    {
        var refresher = new Mock<IAuthenticationSessionRefresher>();
        var bootstrapper = new AuthenticationSessionBootstrapper(refresher.Object);
        await bootstrapper.RestoreAsync();
        refresher.Verify(service => service.TryRefreshAsync(false, null, true), Times.Once);
    }
}
