namespace BlazorShop.Tests.Application.Services
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services;
    using BlazorShop.Application.Validations;
    using BlazorShop.Application.Validations.Seo;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.CategoryPersistence;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Tests.TestUtilities;

    using Moq;

    using Xunit;

    public class CategorySeoServiceTests
    {
        private readonly Mock<IGenericRepository<Category>> _categoryRepository;
        private readonly Mock<ICategoryRepository> _categoryReadRepository;
        private readonly CategorySeoService _service;

        public CategorySeoServiceTests()
        {
            this._categoryRepository = new Mock<IGenericRepository<Category>>();
            this._categoryReadRepository = new Mock<ICategoryRepository>();

            var slugService = new SlugService();
            this._service = new CategorySeoService(
                this._categoryRepository.Object,
                this._categoryReadRepository.Object,
                AutoMapperTestFactory.CreateMapper(),
                slugService,
                new ValidationService(),
                new UpdateCategorySeoDtoValidator(slugService));
        }

        [Fact]
        public async Task GetByCategoryIdAsync_WhenCategoryExists_ReturnsMappedSeoPayload()
        {
            var categoryId = Guid.NewGuid();
            var category = new Category
            {
                Id = categoryId,
                Slug = "mens-shoes",
                MetaTitle = "Men's Shoes",
                IsPublished = true,
            };

            this._categoryRepository
                .Setup(repository => repository.GetByIdAsync(categoryId))
                .ReturnsAsync(category);

            var result = await this._service.GetByCategoryIdAsync(categoryId);

            Assert.True(result.Success);
            Assert.Equal(ServiceResponseType.Success, result.ResponseType);
            Assert.Equal(categoryId, result.Payload!.CategoryId);
            Assert.Equal("mens-shoes", result.Payload.Slug);
        }

        [Fact]
        public async Task UpdateAsync_WhenPayloadIsValid_NormalizesSlugAndUpdatesEntity()
        {
            var categoryId = Guid.NewGuid();
            var existingCategory = new Category { Id = categoryId, Name = "Men", Slug = "old-slug", IsPublished = true };

            this._categoryRepository
                .Setup(repository => repository.GetByIdAsync(categoryId))
                .ReturnsAsync(existingCategory);
            this._categoryReadRepository
                .Setup(repository => repository.CategorySlugExistsAsync("mens-sale", categoryId))
                .ReturnsAsync(false);
            this._categoryRepository
                .Setup(repository => repository.UpdateAsync(existingCategory))
                .ReturnsAsync(1);

            var result = await this._service.UpdateAsync(categoryId, new UpdateCategorySeoDto
            {
                Slug = "Mens Sale",
                MetaTitle = "Men's Sale",
                IsPublished = true,
            });

            Assert.True(result.Success);
            Assert.Equal(ServiceResponseType.Success, result.ResponseType);
            Assert.Equal("mens-sale", existingCategory.Slug);
            Assert.Equal("Men's Sale", existingCategory.MetaTitle);
        }

        [Fact]
        public async Task UpdateAsync_WhenSlugIsDuplicate_ReturnsConflict()
        {
            var categoryId = Guid.NewGuid();
            var existingCategory = new Category { Id = categoryId, Slug = "old-slug", IsPublished = true };

            this._categoryRepository
                .Setup(repository => repository.GetByIdAsync(categoryId))
                .ReturnsAsync(existingCategory);
            this._categoryReadRepository
                .Setup(repository => repository.CategorySlugExistsAsync("mens-sale", categoryId))
                .ReturnsAsync(true);

            var result = await this._service.UpdateAsync(categoryId, new UpdateCategorySeoDto
            {
                Slug = "Mens Sale",
                IsPublished = true,
            });

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.Conflict, result.ResponseType);
            Assert.Equal("Category slug is already in use.", result.Message);
        }

        [Fact]
        public async Task UpdateAsync_WhenCategoryDoesNotExist_ReturnsNotFound()
        {
            var categoryId = Guid.NewGuid();

            this._categoryRepository
                .Setup(repository => repository.GetByIdAsync(categoryId))
                .ReturnsAsync((Category?)null);

            var result = await this._service.UpdateAsync(categoryId, new UpdateCategorySeoDto
            {
                Slug = "mens-sale",
                IsPublished = true,
            });

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.NotFound, result.ResponseType);
            Assert.Equal("Category not found.", result.Message);
        }
    }
}