namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using BlazorShop.API.Controllers;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.AspNetCore.Mvc;

    using Moq;

    using Xunit;

    public class SeoSettingsControllerTests
    {
        [Fact]
        public async Task Get_ReturnsCurrentSeoSettings()
        {
            var service = new Mock<ISeoSettingsService>();
            service
                .Setup(svc => svc.GetCurrentAsync())
                .ReturnsAsync(new SeoSettingsDto { SiteName = "BlazorShop" });

            var controller = new SeoSettingsController(service.Object);

            var result = await controller.Get();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<SeoSettingsDto>(okResult.Value);
            Assert.Equal("BlazorShop", payload.SiteName);
        }
    }
}