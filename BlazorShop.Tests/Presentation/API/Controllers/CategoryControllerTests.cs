namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using BlazorShop.API.Controllers;
    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services.Contracts;

    using Microsoft.AspNetCore.Mvc;

    using Moq;

    using Xunit;

    public class CategoryControllerTests
    {
        [Fact]
        public async Task GetAll_ReturnsPublishedCategories()
        {
            var categoryService = new Mock<ICategoryService>();
            var publicCatalogService = new Mock<IPublicCatalogService>();
            publicCatalogService
                .Setup(service => service.GetPublishedCategoriesAsync())
                .ReturnsAsync([
                    new GetCategory { Id = Guid.NewGuid(), Name = "Featured", Slug = "featured" },
                ]);

            var controller = new CategoryController(categoryService.Object, publicCatalogService.Object);

            var result = await controller.GetAll();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsAssignableFrom<IEnumerable<GetCategory>>(ok.Value);
            Assert.Single(payload);
        }

        [Fact]
        public async Task GetProductsByCategory_ReturnsNotFound_WhenCategoryIsNotPubliclyVisible()
        {
            var categoryService = new Mock<ICategoryService>();
            var publicCatalogService = new Mock<IPublicCatalogService>();
            publicCatalogService
                .Setup(service => service.GetPublishedCategoryByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((GetCategory?)null);

            var controller = new CategoryController(categoryService.Object, publicCatalogService.Object);

            var result = await controller.GetProductsByCategory(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result.Result);
        }
    }
}