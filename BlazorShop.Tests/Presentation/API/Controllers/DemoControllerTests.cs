namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using BlazorShop.API.Controllers;
    using BlazorShop.Application.DTOs.Demo;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts.Demo;
    using BlazorShop.Infrastructure.Demo;

    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Options;

    using Moq;

    using Xunit;

    public sealed class DemoControllerTests
    {
        [Fact]
        public async Task Start_CreatesIsolatedAdminWorkspaceAndSetsSessionCookie()
        {
            var sessionManager = new Mock<IDemoSessionManager>();
            var requestContext = new Mock<IDemoRequestContext>();
            var options = CreateOptions();
            var expiresAtUtc = DateTime.UtcNow.AddMinutes(options.LifetimeMinutes);

            sessionManager.SetupGet(manager => manager.IsEnabled).Returns(true);
            sessionManager
                .Setup(manager => manager.CreateAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DemoSessionStartResult("demo-token", expiresAtUtc));

            var controller = CreateController(sessionManager.Object, requestContext.Object, options);

            var result = await controller.Start(
                new StartDemoSessionRequest { Role = DemoSessionConstants.AdminRole },
                CancellationToken.None);

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var response = Assert.IsType<DemoSessionResponse>(okResult.Value);
            Assert.True(response.Success);
            Assert.Equal(DemoSessionConstants.AdminRole, response.Role);
            Assert.Equal(options.AdminEmail, response.Email);
            Assert.Equal(options.Password, response.Password);
            Assert.Contains(options.CookieName, controller.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", controller.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void End_MarksCurrentWorkspaceForResetAndDeletesCookie()
        {
            var sessionManager = new Mock<IDemoSessionManager>();
            var requestContext = new Mock<IDemoRequestContext>();
            requestContext.SetupGet(context => context.IsDemo).Returns(true);
            var controller = CreateController(sessionManager.Object, requestContext.Object, CreateOptions());

            var result = controller.End();

            Assert.IsType<OkObjectResult>(result);
            requestContext.Verify(context => context.EndAfterRequest(), Times.Once);
            Assert.Contains("blazorshop-demo-session=;", controller.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("expires=", controller.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Start_ReturnsNotFound_WhenDemoModeIsDisabled()
        {
            var sessionManager = new Mock<IDemoSessionManager>();
            var requestContext = new Mock<IDemoRequestContext>();
            sessionManager.SetupGet(manager => manager.IsEnabled).Returns(false);
            var controller = CreateController(sessionManager.Object, requestContext.Object, CreateOptions());

            var result = await controller.Start(
                new StartDemoSessionRequest { Role = DemoSessionConstants.CustomerRole },
                CancellationToken.None);

            Assert.IsType<NotFoundResult>(result.Result);
            sessionManager.Verify(
                manager => manager.CreateAsync(It.IsAny<CancellationToken>()),
                Times.Never);
        }

        private static DemoController CreateController(
            IDemoSessionManager sessionManager,
            IDemoRequestContext requestContext,
            DemoOptions options)
        {
            return new DemoController(sessionManager, requestContext, Options.Create(options))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext(),
                },
            };
        }

        private static DemoOptions CreateOptions()
        {
            return new DemoOptions
            {
                Enabled = true,
                CookieDomain = ".unrealbg.com",
            };
        }
    }
}
