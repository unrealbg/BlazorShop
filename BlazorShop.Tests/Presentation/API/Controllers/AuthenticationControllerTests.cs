namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using BlazorShop.API.Controllers;
    using BlazorShop.API.Options;
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.UserIdentity;
    using BlazorShop.Application.Services.Contracts.Authentication;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;

    using Moq;

    using Xunit;

    public class AuthenticationControllerTests
    {
        [Fact]
        public async Task LoginUser_WhenSuccessful_SetsRefreshTokenCookieAndStripsItFromResponse()
        {
            var authenticationService = new Mock<IAuthenticationService>();
            authenticationService
                .Setup(service => service.LoginUser(It.IsAny<LoginUser>()))
                .ReturnsAsync(new LoginResponse
                {
                    Success = true,
                    Message = "Login successful.",
                    Token = "access-token",
                    RefreshToken = "refresh-token"
                });

            var controller = CreateController(authenticationService.Object);

            var result = await controller.LoginUser(new LoginUser { Email = "john@example.com", Password = "Password123" });

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<LoginResponse>(ok.Value);
            var setCookieHeader = controller.Response.Headers.SetCookie.ToString();

            Assert.True(response.Success);
            Assert.Equal("access-token", response.Token);
            Assert.Equal(string.Empty, response.RefreshToken);
            Assert.Contains("__Host-blazorshop-refresh=refresh-token", setCookieHeader, StringComparison.Ordinal);
            Assert.Contains("httponly", setCookieHeader, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure", setCookieHeader, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task RefreshToken_WhenCookieIsValid_RotatesRefreshTokenCookie()
        {
            var authenticationService = new Mock<IAuthenticationService>();
            authenticationService
                .Setup(service => service.ReviveToken("stale-refresh-token"))
                .ReturnsAsync(new LoginResponse
                {
                    Success = true,
                    Message = "Token revived successfully.",
                    Token = "new-access-token",
                    RefreshToken = "new-refresh-token"
                });

            var controller = CreateController(authenticationService.Object);
            controller.Request.Headers.Cookie = "__Host-blazorshop-refresh=stale-refresh-token";

            var result = await controller.RefreshToken();

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<LoginResponse>(ok.Value);
            var setCookieHeader = controller.Response.Headers.SetCookie.ToString();

            Assert.True(response.Success);
            Assert.Equal("new-access-token", response.Token);
            Assert.Equal(string.Empty, response.RefreshToken);
            Assert.Contains("__Host-blazorshop-refresh=new-refresh-token", setCookieHeader, StringComparison.Ordinal);
        }

        [Fact]
        public async Task RefreshToken_WhenTokenIsInvalid_DeletesRefreshTokenCookie()
        {
            var authenticationService = new Mock<IAuthenticationService>();
            authenticationService
                .Setup(service => service.ReviveToken("stale-refresh-token"))
                .ReturnsAsync(new LoginResponse { Message = "Invalid token." });

            var controller = CreateController(authenticationService.Object);
            controller.Request.Headers.Cookie = "__Host-blazorshop-refresh=stale-refresh-token";

            var result = await controller.RefreshToken();

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            var response = Assert.IsType<LoginResponse>(badRequest.Value);
            var setCookieHeader = controller.Response.Headers.SetCookie.ToString();

            Assert.False(response.Success);
            Assert.Equal("Invalid token.", response.Message);
            Assert.Contains("__Host-blazorshop-refresh=", setCookieHeader, StringComparison.Ordinal);
            Assert.Contains("expires=thu, 01 jan 1970", setCookieHeader, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Logout_WhenCookieIsPresent_RevokesRefreshTokenAndDeletesCookie()
        {
            var authenticationService = new Mock<IAuthenticationService>();
            authenticationService
                .Setup(service => service.Logout("stale-refresh-token"))
                .ReturnsAsync(new ServiceResponse(true, "Logged out successfully."));

            var controller = CreateController(authenticationService.Object);
            controller.Request.Headers.Cookie = "__Host-blazorshop-refresh=stale-refresh-token";

            var result = await controller.Logout();

            var ok = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ServiceResponse>(ok.Value);
            var setCookieHeader = controller.Response.Headers.SetCookie.ToString();

            Assert.True(response.Success);
            Assert.Equal("Logged out successfully.", response.Message);
            Assert.Contains("__Host-blazorshop-refresh=", setCookieHeader, StringComparison.Ordinal);
            Assert.Contains("expires=thu, 01 jan 1970", setCookieHeader, StringComparison.OrdinalIgnoreCase);
            authenticationService.Verify(service => service.Logout("stale-refresh-token"), Times.Once);
        }

        private static AuthenticationController CreateController(IAuthenticationService authenticationService)
        {
            var controller = new AuthenticationController(
                authenticationService,
                Options.Create(new ApiRuntimeOptions()));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            };

            controller.HttpContext.Request.Scheme = "https";
            controller.HttpContext.Request.Host = new HostString("shop.example.com");
            return controller;
        }
    }
}