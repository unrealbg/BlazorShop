namespace BlazorShop.Tests.Application.Services
{
    using AutoMapper;

    using BlazorShop.Application.DTOs.Category;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.CategoryPersistence;
    using BlazorShop.Domain.Entities;

    using Moq;

    using Xunit;

    public class CategoryServiceTests
    {
        private readonly Mock<IGenericRepository<Category>> _mockGenericRepository;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ICategoryRepository> _mockCategoryRepository;
        private readonly CategoryService _categoryService;

        public CategoryServiceTests()
        {
            this._mockGenericRepository = new Mock<IGenericRepository<Category>>();
            this._mockMapper = new Mock<IMapper>();
            this._mockCategoryRepository = new Mock<ICategoryRepository>();
            this._categoryService = new CategoryService(this._mockGenericRepository.Object, this._mockMapper.Object, this._mockCategoryRepository.Object);
        }

        [Fact]
        public async Task GetAllAsync_ShouldReturnCategories()
        {
            // Arrange
            var categories = new List<Category> { new Category { Id = Guid.NewGuid(), Name = "Test Category" } };
            this._mockGenericRepository.Setup(repo => repo.GetAllAsync()).ReturnsAsync(categories);
            this._mockMapper.Setup(m => m.Map<IEnumerable<GetCategory>>(It.IsAny<IEnumerable<Category>>())).Returns(new List<GetCategory> { new GetCategory { Id = categories[0].Id, Name = categories[0].Name } });

            // Act
            var result = await this._categoryService.GetAllAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCategory()
        {
            // Arrange
            var category = new Category { Id = Guid.NewGuid(), Name = "Test Category" };
            this._mockGenericRepository.Setup(repo => repo.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(category);
            this._mockMapper.Setup(m => m.Map<GetCategory>(It.IsAny<Category>())).Returns(new GetCategory { Id = category.Id, Name = category.Name });

            // Act
            var result = await this._categoryService.GetByIdAsync(category.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(category.Id, result.Id);
        }

        [Fact]
        public async Task AddAsync_ShouldReturnSuccessResponse()
        {
            // Arrange
            var createCategory = new CreateCategory { Name = "New Category" };
            this._mockGenericRepository.Setup(repo => repo.AddAsync(It.IsAny<Category>())).ReturnsAsync(1);
            this._mockMapper.Setup(m => m.Map<Category>(It.IsAny<CreateCategory>())).Returns(new Category { Name = createCategory.Name });

            // Act
            var result = await this._categoryService.AddAsync(createCategory);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Category added successfully", result.Message);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReturnSuccessResponse()
        {
            // Arrange
            var updateCategory = new UpdateCategory { Id = Guid.NewGuid(), Name = "Updated Category" };
            this._mockGenericRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Category>())).ReturnsAsync(1);
            this._mockMapper.Setup(m => m.Map<Category>(It.IsAny<UpdateCategory>())).Returns(new Category { Id = updateCategory.Id, Name = updateCategory.Name });

            // Act
            var result = await this._categoryService.UpdateAsync(updateCategory);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Category updated successfully", result.Message);
        }

        [Fact]
        public async Task DeleteAsync_ShouldReturnSuccessResponse()
        {
            // Arrange
            var categoryId = Guid.NewGuid();
            this._mockGenericRepository.Setup(repo => repo.DeleteAsync(It.IsAny<Guid>())).ReturnsAsync(1);

            // Act
            var result = await this._categoryService.DeleteAsync(categoryId);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("Category deleted successfully", result.Message);
        }

        [Fact]
        public async Task GetProductsByCategoryAsync_ShouldReturnProducts()
        {
            // Arrange
            var categoryId = Guid.NewGuid();
            var products = new List<Product> { new Product { Id = Guid.NewGuid(), Name = "Test Product" } };
            this._mockCategoryRepository.Setup(repo => repo.GetProductsByCategoryAsync(It.IsAny<Guid>())).ReturnsAsync(products);
            this._mockMapper.Setup(m => m.Map<IEnumerable<GetProduct>>(It.IsAny<IEnumerable<Product>>())).Returns(new List<GetProduct> { new GetProduct { Id = products[0].Id, Name = products[0].Name } });

            // Act
            var result = await this._categoryService.GetProductsByCategoryAsync(categoryId);

            // Assert
            Assert.NotNull(result);
            Assert.Single(result);
        }
    }
}