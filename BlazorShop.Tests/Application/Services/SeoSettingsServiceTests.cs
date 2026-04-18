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

    public class SeoSettingsServiceTests
    {
        private readonly Mock<ISeoSettingsRepository> _seoSettingsRepository;
        private readonly Mock<IGenericRepository<SeoSettings>> _genericRepository;
        private readonly SeoSettingsService _service;

        public SeoSettingsServiceTests()
        {
            this._seoSettingsRepository = new Mock<ISeoSettingsRepository>();
            this._genericRepository = new Mock<IGenericRepository<SeoSettings>>();

            this._service = new SeoSettingsService(
                this._seoSettingsRepository.Object,
                this._genericRepository.Object,
                AutoMapperTestFactory.CreateMapper(),
                new ValidationService(),
                new UpdateSeoSettingsDtoValidator());
        }

        [Fact]
        public async Task GetCurrentAsync_WhenNoSettingsExist_ReturnsDefaultDto()
        {
            this._seoSettingsRepository
                .Setup(repository => repository.GetCurrentAsync())
                .ReturnsAsync((SeoSettings?)null);

            var result = await this._service.GetCurrentAsync();

            Assert.NotNull(result);
            Assert.Null(result.SiteName);
            Assert.Null(result.BaseCanonicalUrl);
        }

        [Fact]
        public async Task UpdateAsync_WhenNoSettingsExist_CreatesSettingsRow()
        {
            this._seoSettingsRepository
                .Setup(repository => repository.GetCurrentAsync())
                .ReturnsAsync((SeoSettings?)null);
            this._genericRepository
                .Setup(repository => repository.AddAsync(It.IsAny<SeoSettings>()))
                .Callback<SeoSettings>(settings => settings.Id = Guid.NewGuid())
                .ReturnsAsync(1);

            var result = await this._service.UpdateAsync(new UpdateSeoSettingsDto
            {
                SiteName = "BlazorShop",
                BaseCanonicalUrl = "https://shop.example.com",
            });

            Assert.True(result.Success);
            Assert.Equal(ServiceResponseType.Success, result.ResponseType);
            Assert.NotNull(result.Payload);
            Assert.Equal("BlazorShop", result.Payload!.SiteName);
            Assert.Equal("https://shop.example.com", result.Payload.BaseCanonicalUrl);
        }

        [Fact]
        public async Task UpdateAsync_WhenSettingsExist_UpdatesExistingSettings()
        {
            var settings = new SeoSettings { Id = Guid.NewGuid(), SiteName = "Old Name", BaseCanonicalUrl = "https://old.example.com" };

            this._seoSettingsRepository
                .Setup(repository => repository.GetCurrentAsync())
                .ReturnsAsync(settings);
            this._genericRepository
                .Setup(repository => repository.UpdateAsync(settings))
                .ReturnsAsync(1);

            var result = await this._service.UpdateAsync(new UpdateSeoSettingsDto
            {
                SiteName = "New Name",
                BaseCanonicalUrl = "https://shop.example.com",
            });

            Assert.True(result.Success);
            Assert.Equal("New Name", settings.SiteName);
            Assert.Equal("https://shop.example.com", settings.BaseCanonicalUrl);
        }

        [Fact]
        public async Task UpdateAsync_WhenPayloadIsInvalid_ReturnsValidationError()
        {
            var result = await this._service.UpdateAsync(new UpdateSeoSettingsDto
            {
                BaseCanonicalUrl = "not-a-url",
            });

            Assert.False(result.Success);
            Assert.Equal(ServiceResponseType.ValidationError, result.ResponseType);
        }
    }
}