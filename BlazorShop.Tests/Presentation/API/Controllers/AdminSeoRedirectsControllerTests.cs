namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using BlazorShop.API.Controllers;
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.AspNetCore.Mvc;

    using Moq;

    using Xunit;

    public class AdminSeoRedirectsControllerTests
    {
        [Fact]
        public async Task Create_WhenSuccessful_ReturnsCreatedAtAction()
        {
            var redirectId = Guid.NewGuid();
            var service = new Mock<ISeoRedirectService>();
            service
                .Setup(svc => svc.CreateAsync(It.IsAny<UpsertSeoRedirectDto>()))
                .ReturnsAsync(new ServiceResponse<SeoRedirectDto>(true, "SEO redirect created successfully.", redirectId)
                {
                    ResponseType = ServiceResponseType.Success,
                    Payload = new SeoRedirectDto { Id = redirectId, OldPath = "/old", NewPath = "/new" },
                });

            var controller = new AdminSeoRedirectsController(service.Object);

            var result = await controller.Create(new UpsertSeoRedirectDto { OldPath = "/old", NewPath = "/new" });

            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(AdminSeoRedirectsController.GetById), createdResult.ActionName);
        }

        [Fact]
        public async Task Delete_WhenRedirectIsMissing_ReturnsNotFound()
        {
            var service = new Mock<ISeoRedirectService>();
            service
                .Setup(svc => svc.DeleteAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new ServiceResponse<SeoRedirectDto>(false, "Redirect not found.")
                {
                    ResponseType = ServiceResponseType.NotFound,
                });

            var controller = new AdminSeoRedirectsController(service.Object);

            var result = await controller.Delete(Guid.NewGuid());

            Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}