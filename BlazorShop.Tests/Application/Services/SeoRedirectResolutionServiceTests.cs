namespace BlazorShop.Tests.Application.Services
{
    using BlazorShop.Application.Services;
    using BlazorShop.Domain.Contracts.Seo;
    using BlazorShop.Domain.Entities;

    using Moq;

    using Xunit;

    public class SeoRedirectResolutionServiceTests
    {
        private readonly Mock<ISeoRedirectRepository> _seoRedirectRepository;
        private readonly SeoRedirectResolutionService _service;

        public SeoRedirectResolutionServiceTests()
        {
            _seoRedirectRepository = new Mock<ISeoRedirectRepository>();
            _service = new SeoRedirectResolutionService(_seoRedirectRepository.Object);
        }

        [Fact]
        public async Task ResolvePublicPathAsync_WhenSingleRedirectExists_ReturnsResolution()
        {
            _seoRedirectRepository
                .Setup(repository => repository.GetActiveByOldPathAsync("/product/old-slug"))
                .ReturnsAsync(new SeoRedirect
                {
                    OldPath = "/product/old-slug",
                    NewPath = "/product/new-slug",
                    StatusCode = 301,
                    IsActive = true,
                });
            _seoRedirectRepository
                .Setup(repository => repository.GetActiveByOldPathAsync("/product/new-slug"))
                .ReturnsAsync((SeoRedirect?)null);

            var result = await _service.ResolvePublicPathAsync("/product/old-slug");

            Assert.NotNull(result);
            Assert.Equal("/product/new-slug", result!.NewPath);
            Assert.Equal(301, result.StatusCode);
        }

        [Fact]
        public async Task ResolvePublicPathAsync_WhenChainExists_ReturnsFinalDestinationUsingFirstStatusCode()
        {
            _seoRedirectRepository
                .Setup(repository => repository.GetActiveByOldPathAsync("/legacy-sale"))
                .ReturnsAsync(new SeoRedirect
                {
                    OldPath = "/legacy-sale",
                    NewPath = "/sale",
                    StatusCode = 302,
                    IsActive = true,
                });
            _seoRedirectRepository
                .Setup(repository => repository.GetActiveByOldPathAsync("/sale"))
                .ReturnsAsync(new SeoRedirect
                {
                    OldPath = "/sale",
                    NewPath = "/todays-deals",
                    StatusCode = 301,
                    IsActive = true,
                });
            _seoRedirectRepository
                .Setup(repository => repository.GetActiveByOldPathAsync("/todays-deals"))
                .ReturnsAsync((SeoRedirect?)null);

            var result = await _service.ResolvePublicPathAsync("/legacy-sale");

            Assert.NotNull(result);
            Assert.Equal("/todays-deals", result!.NewPath);
            Assert.Equal(302, result.StatusCode);
        }

        [Fact]
        public async Task ResolvePublicPathAsync_WhenLoopIsDetected_ReturnsNull()
        {
            _seoRedirectRepository
                .Setup(repository => repository.GetActiveByOldPathAsync("/loop-a"))
                .ReturnsAsync(new SeoRedirect
                {
                    OldPath = "/loop-a",
                    NewPath = "/loop-b",
                    StatusCode = 301,
                    IsActive = true,
                });
            _seoRedirectRepository
                .Setup(repository => repository.GetActiveByOldPathAsync("/loop-b"))
                .ReturnsAsync(new SeoRedirect
                {
                    OldPath = "/loop-b",
                    NewPath = "/loop-a",
                    StatusCode = 301,
                    IsActive = true,
                });

            var result = await _service.ResolvePublicPathAsync("/loop-a");

            Assert.Null(result);
        }
    }
}