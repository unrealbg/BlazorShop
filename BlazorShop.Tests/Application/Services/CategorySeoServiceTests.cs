namespace BlazorShop.Tests.Application.Services
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services;
    using BlazorShop.Application.Services.Contracts;
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
        private readonly Mock<IApplicationTransactionManager> _transactionManager;
        private readonly Mock<ISeoRedirectAutomationService> _seoRedirectAutomationService;
        private readonly CategorySeoService _service;

        public CategorySeoServiceTests()
        {
            _categoryRepository = new Mock<IGenericRepository<Category>>();
            _categoryReadRepository = new Mock<ICategoryRepository>();
            _transactionManager = new Mock<IApplicationTransactionManager>();
            _seoRedirectAutomationService = new Mock<ISeoRedirectAutomationService>();

            _transactionManager
                .Setup(manager => manager.ExecuteInTransactionAsync(It.IsAny<Func<Task<ServiceResponse<CategorySeoDto>>>>()))
                .Returns((Func<Task<ServiceResponse<CategorySeoDto>>> action) => action());

            var slugService = new SlugService();
            _service = new CategorySeoService(
                _categoryRepository.Object,
                _categoryReadRepository.Object,
                AutoMapperTestFactory.CreateMapper(),
                slugService,
                _transactionManager.Object,
                _seoRedirectAutomationService.Object,
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

            _categoryRepository
                .Setup(repository => repository.GetByIdAsync(categoryId))
                .ReturnsAsync(category);

            var result = await _service.GetByCategoryIdAsync(categoryId);

            Assert.True(result.Success);
            Assert.Equal(ServiceResponseType.Success, result.ResponseType);
            Assert.Equal(categoryId, result.Payload!.CategoryId);
            Assert.Equal("mens-shoes", result.Payload.Slug);
        }

        [Fact]
        public async Task UpdateAsync_WhenPayloadIsValid_NormalizesSlugAndUpdatesEntityAndCreatesRedirect()
        {
            var categoryId = Guid.NewGuid();
            var existingCategory = new Category { Id = categoryId, Name = "Men", Slug = "old-slug", IsPublished = true };

            _categoryRepository
                .Setup(repository => repository.GetByIdAsync(categoryId))
                .ReturnsAsync(existingCategory);
            _categoryReadRepository
                .Setup(repository => repository.CategorySlugExistsAsync("mens-sale", categoryId))
                .ReturnsAsync(false);
            _seoRedirectAutomationService
                .Setup(service => service.EnsurePermanentRedirectAsync("/category/old-slug", "/category/mens-sale"))
                .ReturnsAsync(new ServiceResponse<SeoRedirectDto>(true, "Created", Guid.NewGuid())
                {
                    ResponseType = ServiceResponseType.Success,
                    Payload = new SeoRedirectDto
                    {
                        OldPath = "/category/old-slug",
                        NewPath = "/category/mens-sale",
                        StatusCode = 301,
                        IsActive = true,
                    },
                });
            _categoryRepository
                .Setup(repository => repository.UpdateAsync(existingCategory))
                .ReturnsAsync(1);

            var result = await _service.UpdateAsync(categoryId, new UpdateCategorySeoDto
            {
                Slug = "Mens Sale",
                MetaTitle = "Men's Sale",
                IsPublished = true,
            });

            Assert.True(result.Success);
            Assert.Equal(ServiceResponseType.Success, result.ResponseType);
            Assert.Equal("mens-sale", existingCategory.Slug);
            Assert.Equal("Men's Sale", existingCategory.MetaTitle);
            _seoRedirectAutomationService.Verify(service => service.EnsurePermanentRedirectAsync("/category/old-slug", "/category/mens-sale"), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenSlugIsDuplicate_ReturnsConflict()
        {
            var categoryId = Guid.NewGuid();
            var existingCategory = new Category { Id = categoryId, Slug = "old-slug", IsPublished = true };

            _categoryRepository
                .Setup(repository => repository.GetByIdAsync(categoryId))
                .ReturnsAsync(existingCategory);
            _categoryReadRepository
                .Setup(repository => repository.CategorySlugExistsAsync("mens-sale", categoryId))
                .ReturnsAsync(true);

            var result = await _service.UpdateAsync(categoryId, new UpdateCategorySeoDto
            {
                Slug = "Mens Sale",
                IsPublished = true,
            });

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.Conflict, result.ResponseType);
            Assert.Equal("Category slug is already in use.", result.Message);
            _seoRedirectAutomationService.Verify(service => service.EnsurePermanentRedirectAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenSlugIsUnchanged_DoesNotCreateRedirect()
        {
            var categoryId = Guid.NewGuid();
            var existingCategory = new Category { Id = categoryId, Slug = "mens-sale", IsPublished = true };

            _categoryRepository
                .Setup(repository => repository.GetByIdAsync(categoryId))
                .ReturnsAsync(existingCategory);
            _categoryReadRepository
                .Setup(repository => repository.CategorySlugExistsAsync("mens-sale", categoryId))
                .ReturnsAsync(false);
            _categoryRepository
                .Setup(repository => repository.UpdateAsync(existingCategory))
                .ReturnsAsync(1);

            var result = await _service.UpdateAsync(categoryId, new UpdateCategorySeoDto
            {
                Slug = "mens-sale",
                IsPublished = true,
            });

            Assert.True(result.Success);
            _seoRedirectAutomationService.Verify(service => service.EnsurePermanentRedirectAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UpdateAsync_WhenCategoryDoesNotExist_ReturnsNotFound()
        {
            var categoryId = Guid.NewGuid();

            _categoryRepository
                .Setup(repository => repository.GetByIdAsync(categoryId))
                .ReturnsAsync((Category?)null);

            var result = await _service.UpdateAsync(categoryId, new UpdateCategorySeoDto
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