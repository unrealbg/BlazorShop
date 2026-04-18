namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using BlazorShop.API.Controllers;
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.AspNetCore.Mvc;

    using Moq;

    using Xunit;

    public class AdminProductSeoControllerTests
    {
        [Fact]
        public async Task Get_WhenProductSeoIsMissing_ReturnsNotFound()
        {
            var service = new Mock<IProductSeoService>();
            service
                .Setup(svc => svc.GetByProductIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync(new ServiceResponse<ProductSeoDto>(false, "Product not found.")
                {
                    ResponseType = ServiceResponseType.NotFound,
                });

            var controller = new AdminProductSeoController(service.Object);

            var result = await controller.Get(Guid.NewGuid());

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task Update_WhenSlugConflicts_ReturnsConflict()
        {
            var service = new Mock<IProductSeoService>();
            service
                .Setup(svc => svc.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateProductSeoDto>()))
                .ReturnsAsync(new ServiceResponse<ProductSeoDto>(false, "Product slug is already in use.")
                {
                    ResponseType = ServiceResponseType.Conflict,
                });

            var controller = new AdminProductSeoController(service.Object);

            var result = await controller.Update(Guid.NewGuid(), new UpdateProductSeoDto());

            Assert.IsType<ConflictObjectResult>(result);
        }
    }
}