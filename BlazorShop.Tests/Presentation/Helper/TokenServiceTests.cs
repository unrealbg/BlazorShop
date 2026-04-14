namespace BlazorShop.Tests.Presentation.Helper
{
    using BlazorShop.Web.Shared.BrowserStorage.Contracts;
    using BlazorShop.Web.Shared.Helper;

    using Moq;

    using Xunit;

    public class TokenServiceTests
    {
        [Fact]
        public async Task GetJwtTokenAsync_ReturnsStoredToken()
        {
            var storage = new Mock<IBrowserSessionStorageService>();
            storage.Setup(service => service.GetAsync("token")).ReturnsAsync("jwt-token");
            var tokenService = new TokenService(storage.Object);

            var jwtToken = await tokenService.GetJwtTokenAsync("token");

            Assert.Equal("jwt-token", jwtToken);
        }

        [Fact]
        public async Task StoreJwtTokenAsync_PersistsTokenInSessionStorage()
        {
            var storage = new Mock<IBrowserSessionStorageService>();
            var tokenService = new TokenService(storage.Object);

            await tokenService.StoreJwtTokenAsync("token", "jwt-token");

            storage.Verify(service => service.SetAsync("token", "jwt-token"), Times.Once);
        }

        [Fact]
        public async Task RemoveJwtTokenAsync_RemovesStoredToken()
        {
            var storage = new Mock<IBrowserSessionStorageService>();
            var tokenService = new TokenService(storage.Object);

            await tokenService.RemoveJwtTokenAsync("token");

            storage.Verify(service => service.RemoveAsync("token"), Times.Once);
        }
    }
}