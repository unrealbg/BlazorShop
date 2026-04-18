namespace BlazorShop.Tests.Application.Services
{
    using AutoMapper;

    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.CategoryPersistence;
    using BlazorShop.Domain.Entities;

    using Moq;

    using Xunit;

    public class PublicCatalogServiceTests
    {
        private readonly Mock<ICategoryRepository> _categoryRepository = new();
        private readonly Mock<IMapper> _mapper = new();
        private readonly Mock<IProductReadRepository> _productReadRepository = new();
        private readonly Mock<ISlugService> _slugService = new();

        [Fact]
        public async Task GetPublishedProductBySlugAsync_NormalizesSlugBeforeLookup()
        {
            var product = new Product { Id = Guid.NewGuid(), Name = "Running Shoes", Slug = "running-shoes", IsPublished = true };
            var mappedProduct = new GetProduct { Id = product.Id, Name = product.Name, Slug = product.Slug };

            _slugService.Setup(service => service.NormalizeSlug("Running Shoes")).Returns("running-shoes");
            _productReadRepository.Setup(repository => repository.GetPublishedProductBySlugAsync("running-shoes")).ReturnsAsync(product);
            _mapper.Setup(mapper => mapper.Map<GetProduct>(product)).Returns(mappedProduct);

            var service = CreateService();

            var result = await service.GetPublishedProductBySlugAsync("Running Shoes");

            Assert.NotNull(result);
            Assert.Equal("running-shoes", result!.Slug);
            _productReadRepository.Verify(repository => repository.GetPublishedProductBySlugAsync("running-shoes"), Times.Once);
        }

        [Fact]
        public async Task GetPublishedCategoryPageBySlugAsync_ReturnsCategoryAndProducts()
        {
            var category = new Category { Id = Guid.NewGuid(), Name = "Shoes", Slug = "shoes", IsPublished = true };
            var products = new List<CatalogProductReadModel>
            {
                new() { Id = Guid.NewGuid(), Name = "Running Shoes", Slug = "running-shoes", CategoryId = category.Id },
            };
            var mappedCategory = new GetCategory { Id = category.Id, Name = category.Name, Slug = category.Slug };
            var mappedProducts = new List<GetCatalogProduct>
            {
                new() { Id = products[0].Id, Name = products[0].Name, Slug = products[0].Slug, CategoryId = category.Id },
            };

            _slugService.Setup(service => service.NormalizeSlug("Shoes")).Returns("shoes");
            _categoryRepository.Setup(repository => repository.GetPublishedCategoryBySlugAsync("shoes")).ReturnsAsync(category);
            _productReadRepository.Setup(repository => repository.GetPublishedProductsByCategoryAsync(category.Id)).ReturnsAsync(products);
            _mapper.Setup(mapper => mapper.Map<GetCategory>(category)).Returns(mappedCategory);
            _mapper.Setup(mapper => mapper.Map<IReadOnlyList<GetCatalogProduct>>(products)).Returns(mappedProducts);

            var service = CreateService();

            var result = await service.GetPublishedCategoryPageBySlugAsync("Shoes");

            Assert.NotNull(result);
            Assert.Equal("shoes", result!.Category.Slug);
            Assert.Single(result.Products);
            Assert.Equal("running-shoes", result.Products[0].Slug);
        }

        [Fact]
        public async Task GetPublishedCategoryPageBySlugAsync_ReturnsNullWhenCategoryIsMissing()
        {
            _slugService.Setup(service => service.NormalizeSlug("Missing Category")).Returns("missing-category");
            _categoryRepository.Setup(repository => repository.GetPublishedCategoryBySlugAsync("missing-category")).ReturnsAsync((Category?)null);

            var service = CreateService();

            var result = await service.GetPublishedCategoryPageBySlugAsync("Missing Category");

            Assert.Null(result);
            _productReadRepository.Verify(repository => repository.GetPublishedProductsByCategoryAsync(It.IsAny<Guid>()), Times.Never);
        }

        private PublicCatalogService CreateService()
        {
            return new PublicCatalogService(
                _categoryRepository.Object,
                _mapper.Object,
                _productReadRepository.Object,
                _slugService.Object);
        }
    }
}