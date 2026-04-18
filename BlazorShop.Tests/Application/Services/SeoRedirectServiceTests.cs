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

    public class SeoRedirectServiceTests
    {
        private readonly Mock<IGenericRepository<SeoRedirect>> _genericRepository;
        private readonly Mock<ISeoRedirectRepository> _seoRedirectRepository;
        private readonly SeoRedirectService _service;

        public SeoRedirectServiceTests()
        {
            this._genericRepository = new Mock<IGenericRepository<SeoRedirect>>();
            this._seoRedirectRepository = new Mock<ISeoRedirectRepository>();

            this._service = new SeoRedirectService(
                this._genericRepository.Object,
                this._seoRedirectRepository.Object,
                AutoMapperTestFactory.CreateMapper(),
                new ValidationService(),
                new SeoRedirectDtoValidator());
        }

        [Fact]
        public async Task GetAllAsync_ReturnsRedirectsOrderedByCreatedOnDescending()
        {
            var older = new SeoRedirect { Id = Guid.NewGuid(), OldPath = "/old", NewPath = "/new", CreatedOn = DateTime.UtcNow.AddDays(-1) };
            var newer = new SeoRedirect { Id = Guid.NewGuid(), OldPath = "/older", NewPath = "/newer", CreatedOn = DateTime.UtcNow };

            this._genericRepository
                .Setup(repository => repository.GetAllAsync())
                .ReturnsAsync(new[] { older, newer });

            var result = await this._service.GetAllAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal(newer.Id, result[0].Id);
            Assert.Equal(older.Id, result[1].Id);
        }

        [Fact]
        public async Task CreateAsync_WhenOldPathIsDuplicate_ReturnsConflict()
        {
            this._seoRedirectRepository
                .Setup(repository => repository.OldPathExistsAsync("/products/running-shoes", null))
                .ReturnsAsync(true);

            var result = await this._service.CreateAsync(new UpsertSeoRedirectDto
            {
                OldPath = "/products/running-shoes",
                NewPath = "/products/running-shoes-2",
                StatusCode = 301,
            });

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.Conflict, result.ResponseType);
            Assert.Equal("Redirect old path is already in use.", result.Message);
        }

        [Fact]
        public async Task CreateAsync_WhenValid_CreatesRedirect()
        {
            this._seoRedirectRepository
                .Setup(repository => repository.OldPathExistsAsync("/products/old-running-shoes", null))
                .ReturnsAsync(false);
            this._genericRepository
                .Setup(repository => repository.AddAsync(It.IsAny<SeoRedirect>()))
                .Callback<SeoRedirect>(redirect =>
                {
                    redirect.Id = Guid.NewGuid();
                    redirect.CreatedOn = DateTime.UtcNow;
                })
                .ReturnsAsync(1);

            var result = await this._service.CreateAsync(new UpsertSeoRedirectDto
            {
                OldPath = "/products/old-running-shoes",
                NewPath = "/products/running-shoes",
                StatusCode = 301,
            });

            Assert.True(result.Success);
            Assert.Equal(ServiceResponseType.Success, result.ResponseType);
            Assert.NotNull(result.Payload);
            Assert.Equal("/products/old-running-shoes", result.Payload!.OldPath);
        }

        [Fact]
        public async Task UpdateAsync_WhenRedirectDoesNotExist_ReturnsNotFound()
        {
            var redirectId = Guid.NewGuid();

            this._genericRepository
                .Setup(repository => repository.GetByIdAsync(redirectId))
                .ReturnsAsync((SeoRedirect?)null);

            var result = await this._service.UpdateAsync(redirectId, new UpsertSeoRedirectDto
            {
                OldPath = "/products/old-running-shoes",
                NewPath = "/products/running-shoes",
                StatusCode = 301,
            });

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.NotFound, result.ResponseType);
        }

        [Fact]
        public async Task DeactivateAsync_WhenRedirectExists_SetsInactive()
        {
            var redirectId = Guid.NewGuid();
            var redirect = new SeoRedirect { Id = redirectId, OldPath = "/old", NewPath = "/new", IsActive = true };

            this._genericRepository
                .Setup(repository => repository.GetByIdAsync(redirectId))
                .ReturnsAsync(redirect);
            this._genericRepository
                .Setup(repository => repository.UpdateAsync(redirect))
                .ReturnsAsync(1);

            var result = await this._service.DeactivateAsync(redirectId);

            Assert.True(result.Success);
            Assert.False(redirect.IsActive);
            Assert.Equal(ServiceResponseType.Success, result.ResponseType);
        }

        [Fact]
        public async Task DeleteAsync_WhenRedirectExists_DeletesRedirect()
        {
            var redirectId = Guid.NewGuid();
            var redirect = new SeoRedirect { Id = redirectId, OldPath = "/old", NewPath = "/new", IsActive = true };

            this._genericRepository
                .Setup(repository => repository.GetByIdAsync(redirectId))
                .ReturnsAsync(redirect);
            this._genericRepository
                .Setup(repository => repository.DeleteAsync(redirectId))
                .ReturnsAsync(1);

            var result = await this._service.DeleteAsync(redirectId);

            Assert.True(result.Success);
            Assert.Equal(ServiceResponseType.Success, result.ResponseType);
            Assert.Equal(redirectId, result.Payload!.Id);
        }
    }
}