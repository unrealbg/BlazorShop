namespace BlazorShop.Tests.Application.Services
{
    using BlazorShop.Application.Diagnostics;
    using BlazorShop.Application.Services;
    using BlazorShop.Domain.Contracts.Seo;
    using BlazorShop.Domain.Entities;

    using BlazorShop.Tests.Support.Logging;

    using Microsoft.Extensions.Logging;

    using Moq;

    using Xunit;

    public class SeoRedirectResolutionServiceTests : IDisposable
    {
        private readonly Mock<ISeoRedirectRepository> _seoRedirectRepository;
        private readonly ILoggerFactory _loggerFactory;
        private readonly TestLogSink _logSink;
        private readonly SeoRedirectResolutionService _service;

        public SeoRedirectResolutionServiceTests()
        {
            _seoRedirectRepository = new Mock<ISeoRedirectRepository>();
            _logSink = new TestLogSink();
            _loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(_logSink.CreateProvider()));
            _service = new SeoRedirectResolutionService(_seoRedirectRepository.Object, _loggerFactory.CreateLogger<SeoRedirectResolutionService>());
        }

        public void Dispose()
        {
            _loggerFactory.Dispose();
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
            Assert.Contains(_logSink.Entries, entry =>
                entry.EventId.Id == 7008
                && entry.LogLevel == LogLevel.Warning
                && entry.GetString("SeoEvent") == SeoRuntimeEventNames.PublicRedirectLoopBlocked
                && entry.GetString("SourcePath") == "/loop-a");
        }

        [Fact]
        public async Task ResolvePublicPathAsync_WhenInvalidTargetIsReturned_LogsInvalidTargetEvent()
        {
            _seoRedirectRepository
                .Setup(repository => repository.GetActiveByOldPathAsync("/legacy-sale"))
                .ReturnsAsync(new SeoRedirect
                {
                    OldPath = "/legacy-sale",
                    NewPath = "https://bad.example.com/external",
                    StatusCode = 301,
                    IsActive = true,
                });

            var result = await _service.ResolvePublicPathAsync("/legacy-sale");

            Assert.Null(result);
            Assert.Contains(_logSink.Entries, entry =>
                entry.EventId.Id == 7010
                && entry.LogLevel == LogLevel.Warning
                && entry.GetString("SeoEvent") == SeoRuntimeEventNames.PublicRedirectInvalidTargetBlocked
                && entry.GetString("SourcePath") == "/legacy-sale"
                && entry.GetString("TargetPath") == "https://bad.example.com/external");
        }

        [Fact]
        public async Task ResolvePublicPathAsync_WhenRedirectChainExceedsLimit_LogsChainBlockedEvent()
        {
            _seoRedirectRepository
                .Setup(repository => repository.GetActiveByOldPathAsync(It.IsAny<string>()))
                .ReturnsAsync((string path) =>
                {
                    if (!path.StartsWith("/chain-", StringComparison.Ordinal))
                    {
                        return null;
                    }

                    var hopNumber = int.Parse(path[7..]);
                    return new SeoRedirect
                    {
                        OldPath = path,
                        NewPath = $"/chain-{hopNumber + 1}",
                        StatusCode = 301,
                        IsActive = true,
                    };
                });

            var result = await _service.ResolvePublicPathAsync("/chain-0");

            Assert.Null(result);
            Assert.Contains(_logSink.Entries, entry =>
                entry.EventId.Id == 7009
                && entry.LogLevel == LogLevel.Warning
                && entry.GetString("SeoEvent") == SeoRuntimeEventNames.PublicRedirectChainBlocked
                && entry.GetString("SourcePath") == "/chain-0"
                && entry.GetInt32("MaxHops") == 10);
        }
    }
}