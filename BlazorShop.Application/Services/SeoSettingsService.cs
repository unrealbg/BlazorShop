namespace BlazorShop.Application.Services
{
    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;
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

        public SeoSettingsService(
            ISeoSettingsRepository seoSettingsRepository,
            IGenericRepository<SeoSettings> genericRepository,
            IMapper mapper,
            IValidationService validationService,
            IValidator<UpdateSeoSettingsDto> validator)
        {
            _seoSettingsRepository = seoSettingsRepository;
            _genericRepository = genericRepository;
            _mapper = mapper;
            _validationService = validationService;
            _validator = validator;
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

            return new ServiceResponse<SeoSettingsDto>(true, "SEO settings updated successfully.", currentSettings.Id)
            {
                Payload = _mapper.Map<SeoSettingsDto>(currentSettings),
                ResponseType = ServiceResponseType.Success,
            };
        }
    }
}