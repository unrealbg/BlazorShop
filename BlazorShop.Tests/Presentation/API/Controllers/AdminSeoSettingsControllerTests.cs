namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using BlazorShop.API.Controllers;
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.AspNetCore.Mvc;

    using Moq;

    using Xunit;

    public class AdminSeoSettingsControllerTests
    {
        [Fact]
        public async Task Get_ReturnsCurrentSeoSettings()
        {
            var service = new Mock<ISeoSettingsService>();
            service
                .Setup(svc => svc.GetCurrentAsync())
                .ReturnsAsync(new SeoSettingsDto { SiteName = "BlazorShop" });

            var controller = new AdminSeoSettingsController(service.Object);

            var result = await controller.Get();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<SeoSettingsDto>(okResult.Value);
            Assert.Equal("BlazorShop", payload.SiteName);
        }

        [Fact]
        public async Task Update_WhenPayloadIsInvalid_ReturnsBadRequest()
        {
            var service = new Mock<ISeoSettingsService>();
            service
                .Setup(svc => svc.UpdateAsync(It.IsAny<UpdateSeoSettingsDto>()))
                .ReturnsAsync(new ServiceResponse<SeoSettingsDto>(false, "BaseCanonicalUrl must be an absolute http/https URL.")
                {
                    ResponseType = ServiceResponseType.ValidationError,
                });

            var controller = new AdminSeoSettingsController(service.Object);

            var result = await controller.Update(new UpdateSeoSettingsDto());

            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}