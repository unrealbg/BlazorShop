namespace BlazorShop.Tests.Application.Services
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services;
    using BlazorShop.Application.Validations;
    using BlazorShop.Application.Validations.Seo;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Seo;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Tests.TestUtilities;

    using Moq;

    using Xunit;

    public class SeoRedirectAutomationServiceTests
    {
        private readonly Mock<IGenericRepository<SeoRedirect>> _genericRepository;
        private readonly Mock<ISeoRedirectRepository> _seoRedirectRepository;
        private readonly SeoRedirectAutomationService _service;

        public SeoRedirectAutomationServiceTests()
        {
            _genericRepository = new Mock<IGenericRepository<SeoRedirect>>();
            _seoRedirectRepository = new Mock<ISeoRedirectRepository>();
            _service = new SeoRedirectAutomationService(
                _genericRepository.Object,
                _seoRedirectRepository.Object,
                AutoMapperTestFactory.CreateMapper(),
                new ValidationService(),
                new SeoRedirectDtoValidator());
        }

        [Fact]
        public async Task EnsurePermanentRedirectAsync_WhenValid_CreatesPermanentRedirect()
        {
            _seoRedirectRepository
                .Setup(repository => repository.GetActiveByOldPathAsync("/product/new-slug"))
                .ReturnsAsync((SeoRedirect?)null);
            _seoRedirectRepository
                .Setup(repository => repository.GetByOldPathAsync("/product/old-slug"))
                .ReturnsAsync((SeoRedirect?)null);
            _genericRepository
                .Setup(repository => repository.AddAsync(It.IsAny<SeoRedirect>()))
                .Callback<SeoRedirect>(redirect => redirect.Id = Guid.NewGuid())
                .ReturnsAsync(1);

            var result = await _service.EnsurePermanentRedirectAsync("/product/old-slug", "/product/new-slug");

            Assert.True(result.Success);
            Assert.Equal(ServiceResponseType.Success, result.ResponseType);
            Assert.Equal(301, result.Payload!.StatusCode);
            Assert.Equal("/product/old-slug", result.Payload.OldPath);
            Assert.Equal("/product/new-slug", result.Payload.NewPath);
        }

        [Fact]
        public async Task EnsurePermanentRedirectAsync_WhenExactActiveRedirectExists_ReusesExistingRedirect()
        {
            var existingRedirect = new SeoRedirect
            {
                Id = Guid.NewGuid(),
                OldPath = "/category/old-slug",
                NewPath = "/category/new-slug",
                StatusCode = 301,
                IsActive = true,
            };

            _seoRedirectRepository
                .Setup(repository => repository.GetActiveByOldPathAsync("/category/new-slug"))
                .ReturnsAsync((SeoRedirect?)null);
            _seoRedirectRepository
                .Setup(repository => repository.GetByOldPathAsync("/category/old-slug"))
                .ReturnsAsync(existingRedirect);

            var result = await _service.EnsurePermanentRedirectAsync("/category/old-slug", "/category/new-slug");

            Assert.True(result.Success);
            Assert.Equal(existingRedirect.Id, result.Payload!.Id);
            _genericRepository.Verify(repository => repository.AddAsync(It.IsAny<SeoRedirect>()), Times.Never);
        }

        [Fact]
        public async Task EnsurePermanentRedirectAsync_WhenOldPathAlreadyManagedByDifferentRedirect_ReturnsConflict()
        {
            _seoRedirectRepository
                .Setup(repository => repository.GetActiveByOldPathAsync("/product/new-slug"))
                .ReturnsAsync((SeoRedirect?)null);
            _seoRedirectRepository
                .Setup(repository => repository.GetByOldPathAsync("/product/old-slug"))
                .ReturnsAsync(new SeoRedirect
                {
                    Id = Guid.NewGuid(),
                    OldPath = "/product/old-slug",
                    NewPath = "/product/elsewhere",
                    IsActive = true,
                });

            var result = await _service.EnsurePermanentRedirectAsync("/product/old-slug", "/product/new-slug");

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.Conflict, result.ResponseType);
            Assert.Equal("Automatic redirect could not be created because the old path is already managed by an existing redirect.", result.Message);
        }

        [Fact]
        public async Task EnsurePermanentRedirectAsync_WhenTargetPathIsClaimedByActiveRedirect_ReturnsConflict()
        {
            _seoRedirectRepository
                .Setup(repository => repository.GetActiveByOldPathAsync("/category/new-slug"))
                .ReturnsAsync(new SeoRedirect
                {
                    Id = Guid.NewGuid(),
                    OldPath = "/category/new-slug",
                    NewPath = "/category/final-slug",
                    IsActive = true,
                });

            var result = await _service.EnsurePermanentRedirectAsync("/category/old-slug", "/category/new-slug");

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.Conflict, result.ResponseType);
            Assert.Equal("Automatic redirect could not be created because the target path is already claimed by an active redirect.", result.Message);
        }

        [Fact]
        public async Task EnsurePermanentRedirectAsync_WhenPathsAreIdentical_ReturnsValidationError()
        {
            var result = await _service.EnsurePermanentRedirectAsync("/product/same-slug", "/product/same-slug");

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.ValidationError, result.ResponseType);
            Assert.Equal("OldPath and NewPath must be different.", result.Message);
        }
    }
}