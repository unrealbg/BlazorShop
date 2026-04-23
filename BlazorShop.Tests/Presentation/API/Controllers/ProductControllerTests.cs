namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using BlazorShop.API.Controllers;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Domain.Contracts;

    using Microsoft.AspNetCore.Mvc;

    using Moq;

    using Xunit;

    public class ProductControllerTests
    {
        [Fact]
        public async Task GetCatalog_ReturnsOkWithPagedCatalog()
        {
            var productService = new Mock<IProductService>();
            var publicCatalogService = new Mock<IPublicCatalogService>();
            var page = new PagedResult<GetCatalogProduct>
            {
                Items =
                [
                    new GetCatalogProduct
                    {
                        Id = Guid.NewGuid(),
                        Name = "Catalog Product",
                        Description = "Catalog Description",
                        Price = 20m,
                        Image = "/img/catalog.png",
                        CreatedOn = DateTime.UtcNow,
                        CategoryId = Guid.NewGuid(),
                        HasVariants = false,
                    }
                ],
                PageNumber = 1,
                PageSize = 12,
                TotalCount = 1,
            };

            publicCatalogService
                .Setup(service => service.GetPublishedCatalogPageAsync(It.IsAny<ProductCatalogQuery>()))
                .ReturnsAsync(page);

            var controller = new ProductController(productService.Object, publicCatalogService.Object);

            var result = await controller.GetCatalog(new ProductCatalogQuery { PageNumber = 1, PageSize = 12 });

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<PagedResult<GetCatalogProduct>>(okResult.Value);
            Assert.Single(payload.Items);
        }

        [Fact]
        public async Task GetSingle_ReturnsNotFound_WhenProductIsNotPubliclyVisible()
        {
            var productService = new Mock<IProductService>();
            var publicCatalogService = new Mock<IPublicCatalogService>();
            publicCatalogService.Setup(service => service.GetPublishedProductByIdAsync(It.IsAny<Guid>())).ReturnsAsync((GetProduct?)null);

            var controller = new ProductController(productService.Object, publicCatalogService.Object);

            var result = await controller.GetSingle(Guid.NewGuid());

            Assert.IsType<NotFoundResult>(result.Result);
        }
    }
}