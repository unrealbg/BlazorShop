namespace BlazorShop.Tests.Presentation.API.Controllers
{
    using BlazorShop.API.Controllers;
    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.DTOs.Discovery;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Domain.Contracts;

    using Microsoft.AspNetCore.Mvc;

    using Moq;

    using Xunit;

    public class PublicCatalogControllerTests
    {
        [Fact]
        public async Task GetProductBySlug_ReturnsNotFound_WhenSlugDoesNotResolve()
        {
            var service = new Mock<IPublicCatalogService>();
            service.Setup(svc => svc.GetPublishedProductBySlugAsync("missing-product")).ReturnsAsync((GetProduct?)null);

            var controller = new PublicCatalogController(service.Object);

            var result = await controller.GetProductBySlug("missing-product");

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetCategoryBySlug_ReturnsCategoryPage_WhenSlugExists()
        {
            var service = new Mock<IPublicCatalogService>();
            service.Setup(svc => svc.GetPublishedCategoryPageBySlugAsync("shoes")).ReturnsAsync(new GetCategoryPage
            {
                Category = new GetCategory { Id = Guid.NewGuid(), Name = "Shoes", Slug = "shoes" },
                Products = [new GetCatalogProduct { Id = Guid.NewGuid(), Name = "Running Shoes", Slug = "running-shoes", CategoryId = Guid.NewGuid() }],
            });

            var controller = new PublicCatalogController(service.Object);

            var result = await controller.GetCategoryBySlug("shoes");

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<GetCategoryPage>(okResult.Value);
            Assert.Equal("shoes", payload.Category.Slug);
            Assert.Single(payload.Products);
        }

        [Fact]
        public async Task GetProducts_ReturnsPublishedCatalogPage()
        {
            var service = new Mock<IPublicCatalogService>();
            service.Setup(svc => svc.GetPublishedCatalogPageAsync(It.IsAny<ProductCatalogQuery>())).ReturnsAsync(new PagedResult<GetCatalogProduct>
            {
                Items = [new GetCatalogProduct { Id = Guid.NewGuid(), Name = "Running Shoes", Slug = "running-shoes", CategoryId = Guid.NewGuid() }],
                PageNumber = 1,
                PageSize = 12,
                TotalCount = 1,
            });

            var controller = new PublicCatalogController(service.Object);

            var result = await controller.GetProducts(new ProductCatalogQuery { PageNumber = 1, PageSize = 12 });

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<PagedResult<GetCatalogProduct>>(okResult.Value);
            Assert.Single(payload.Items);
            Assert.Equal("running-shoes", payload.Items[0].Slug);
        }

        [Fact]
        public async Task GetSitemap_ReturnsPublishedCatalogSitemap()
        {
            var service = new Mock<IPublicCatalogService>();
            service.Setup(svc => svc.GetPublishedSitemapAsync()).ReturnsAsync(new GetPublicCatalogSitemap
            {
                Categories = [new GetCategorySitemapEntry { Slug = "sneakers", LastModifiedUtc = new DateTime(2026, 4, 14, 0, 0, 0, DateTimeKind.Utc) }],
                Products = [new GetProductSitemapEntry { Slug = "metro-runner", LastModifiedUtc = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc) }],
            });

            var controller = new PublicCatalogController(service.Object);

            var result = await controller.GetSitemap();

            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<GetPublicCatalogSitemap>(okResult.Value);
            Assert.Single(payload.Categories);
            Assert.Single(payload.Products);
            Assert.Equal("sneakers", payload.Categories[0].Slug);
            Assert.Equal("metro-runner", payload.Products[0].Slug);
        }
    }
}