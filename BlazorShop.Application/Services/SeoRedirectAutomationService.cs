namespace BlazorShop.Application.Services
{
    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Application.Validations;
    using BlazorShop.Domain.Constants;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Seo;
    using BlazorShop.Domain.Entities;

    using FluentValidation;

    public class SeoRedirectAutomationService : ISeoRedirectAutomationService
    {
        private const string ExistingRedirectConflictMessage = "Automatic redirect could not be created because the old path is already managed by an existing redirect.";
        private const string TargetPathConflictMessage = "Automatic redirect could not be created because the target path is already claimed by an active redirect.";

        private readonly IGenericRepository<SeoRedirect> _genericRepository;
        private readonly ISeoRedirectRepository _seoRedirectRepository;
        private readonly IMapper _mapper;
        private readonly IValidationService _validationService;
        private readonly IValidator<SeoRedirectDto> _validator;

        public SeoRedirectAutomationService(
            IGenericRepository<SeoRedirect> genericRepository,
            ISeoRedirectRepository seoRedirectRepository,
            IMapper mapper,
            IValidationService validationService,
            IValidator<SeoRedirectDto> validator)
        {
            _genericRepository = genericRepository;
            _seoRedirectRepository = seoRedirectRepository;
            _mapper = mapper;
            _validationService = validationService;
            _validator = validator;
        }

        public async Task<ServiceResponse<SeoRedirectDto>> EnsurePermanentRedirectAsync(string oldPath, string newPath)
        {
            var normalizedOldPath = SeoRedirectPathUtility.NormalizePath(oldPath);
            var normalizedNewPath = SeoRedirectPathUtility.NormalizePath(newPath);

            var redirectDto = new SeoRedirectDto
            {
                OldPath = normalizedOldPath,
                NewPath = normalizedNewPath,
                StatusCode = SeoConstraints.PermanentRedirectStatusCode,
                IsActive = true,
            };

            var validationResult = await _validationService.ValidateAsync(redirectDto, _validator);
            if (!validationResult.Success)
            {
                return ValidationError(validationResult.Message ?? "Invalid redirect payload.");
            }

            var targetPathRedirect = await _seoRedirectRepository.GetActiveByOldPathAsync(normalizedNewPath!);
            if (targetPathRedirect is not null)
            {
                return Conflict(TargetPathConflictMessage);
            }

            var existingRedirect = await _seoRedirectRepository.GetByOldPathAsync(normalizedOldPath!);
            if (existingRedirect is not null)
            {
                if (existingRedirect.IsActive && SeoRedirectPathUtility.PathsEqual(existingRedirect.NewPath, normalizedNewPath))
                {
                    return Success(_mapper.Map<SeoRedirectDto>(existingRedirect), existingRedirect.Id, "Existing SEO redirect reused.");
                }

                return Conflict(ExistingRedirectConflictMessage);
            }

            var redirect = new SeoRedirect
            {
                OldPath = normalizedOldPath,
                NewPath = normalizedNewPath,
                StatusCode = SeoConstraints.PermanentRedirectStatusCode,
                IsActive = true,
            };

            var rowsAffected = await _genericRepository.AddAsync(redirect);
            if (rowsAffected <= 0)
            {
                return Failure("Automatic redirect could not be created.");
            }

            return Success(_mapper.Map<SeoRedirectDto>(redirect), redirect.Id, "Automatic SEO redirect created successfully.");
        }

        private static ServiceResponse<SeoRedirectDto> Success(SeoRedirectDto payload, Guid id, string message)
        {
            return new ServiceResponse<SeoRedirectDto>(true, message, id)
            {
                Payload = payload,
                ResponseType = ServiceResponseType.Success,
            };
        }

        private static ServiceResponse<SeoRedirectDto> ValidationError(string message)
        {
            return new ServiceResponse<SeoRedirectDto>(false, message)
            {
                ResponseType = ServiceResponseType.ValidationError,
            };
        }

        private static ServiceResponse<SeoRedirectDto> Conflict(string message)
        {
            return new ServiceResponse<SeoRedirectDto>(false, message)
            {
                ResponseType = ServiceResponseType.Conflict,
            };
        }

        private static ServiceResponse<SeoRedirectDto> Failure(string message)
        {
            return new ServiceResponse<SeoRedirectDto>(false, message)
            {
                ResponseType = ServiceResponseType.Failure,
            };
        }
    }
}