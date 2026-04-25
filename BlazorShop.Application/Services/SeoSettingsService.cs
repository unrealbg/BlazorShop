namespace BlazorShop.Application.Services
{
    using System.Text.Json;

    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Application.Services.Contracts.Admin;
    using BlazorShop.Application.Validations;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Seo;
    using BlazorShop.Domain.Entities;

    using FluentValidation;

    public class SeoSettingsService : ISeoSettingsService
    {
        private readonly ISeoSettingsRepository _seoSettingsRepository;
        private readonly IGenericRepository<SeoSettings> _genericRepository;
        private readonly IMapper _mapper;
        private readonly IValidationService _validationService;
        private readonly IValidator<UpdateSeoSettingsDto> _validator;
        private readonly IAdminAuditService? _auditService;

        public SeoSettingsService(
            ISeoSettingsRepository seoSettingsRepository,
            IGenericRepository<SeoSettings> genericRepository,
            IMapper mapper,
            IValidationService validationService,
            IValidator<UpdateSeoSettingsDto> validator,
            IAdminAuditService? auditService = null)
        {
            _seoSettingsRepository = seoSettingsRepository;
            _genericRepository = genericRepository;
            _mapper = mapper;
            _validationService = validationService;
            _validator = validator;
            _auditService = auditService;
        }

        public async Task<SeoSettingsDto> GetCurrentAsync()
        {
            var settings = await _seoSettingsRepository.GetCurrentAsync();
            return settings is null ? new SeoSettingsDto() : _mapper.Map<SeoSettingsDto>(settings);
        }

        public async Task<ServiceResponse<SeoSettingsDto>> UpdateAsync(UpdateSeoSettingsDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var validationResult = await _validationService.ValidateAsync(request, _validator);

            if (!validationResult.Success)
            {
                return new ServiceResponse<SeoSettingsDto>(false, validationResult.Message)
                {
                    ResponseType = ServiceResponseType.ValidationError,
                };
            }

            var currentSettings = await _seoSettingsRepository.GetCurrentAsync();

            if (currentSettings is null)
            {
                var newSettings = _mapper.Map<SeoSettings>(request);
                var addResult = await _genericRepository.AddAsync(newSettings);

                if (addResult <= 0)
                {
                    return new ServiceResponse<SeoSettingsDto>(false, "SEO settings could not be created.")
                    {
                        ResponseType = ServiceResponseType.Failure,
                    };
                }

                await LogAsync(newSettings, "SEOSettings.Created", "SEO settings created.");

                return new ServiceResponse<SeoSettingsDto>(true, "SEO settings created successfully.", newSettings.Id)
                {
                    Payload = _mapper.Map<SeoSettingsDto>(newSettings),
                    ResponseType = ServiceResponseType.Success,
                };
            }

            _mapper.Map(request, currentSettings);
            var updateResult = await _genericRepository.UpdateAsync(currentSettings);

            if (updateResult <= 0)
            {
                return new ServiceResponse<SeoSettingsDto>(false, "SEO settings could not be updated.")
                {
                    ResponseType = ServiceResponseType.Failure,
                };
            }

            await LogAsync(currentSettings, "SEOSettings.Updated", "SEO settings updated.");

            return new ServiceResponse<SeoSettingsDto>(true, "SEO settings updated successfully.", currentSettings.Id)
            {
                Payload = _mapper.Map<SeoSettingsDto>(currentSettings),
                ResponseType = ServiceResponseType.Success,
            };
        }

        private async Task LogAsync(SeoSettings settings, string action, string summary)
        {
            if (_auditService is null)
            {
                return;
            }

            await _auditService.LogAsync(new CreateAdminAuditLogDto
            {
                Action = action,
                EntityType = "SeoSettings",
                EntityId = settings.Id.ToString(),
                Summary = summary,
                MetadataJson = JsonSerializer.Serialize(new { settings.SiteName, settings.BaseCanonicalUrl }),
            });
        }
    }
}
