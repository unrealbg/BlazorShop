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

    public class SeoRedirectService : ISeoRedirectService
    {
        private readonly IGenericRepository<SeoRedirect> _genericRepository;
        private readonly ISeoRedirectRepository _seoRedirectRepository;
        private readonly IMapper _mapper;
        private readonly IValidationService _validationService;
        private readonly IValidator<SeoRedirectDto> _validator;

        public SeoRedirectService(
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

        public async Task<IReadOnlyList<SeoRedirectDto>> GetAllAsync()
        {
            var redirects = await _genericRepository.GetAllAsync();
            return _mapper.Map<List<SeoRedirectDto>>(redirects.OrderByDescending(redirect => redirect.CreatedOn).ThenBy(redirect => redirect.OldPath));
        }

        public async Task<ServiceResponse<SeoRedirectDto>> GetByIdAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                return ValidationError("Redirect id is required.");
            }

            var redirect = await _genericRepository.GetByIdAsync(id);

            if (redirect is null)
            {
                return NotFound("Redirect not found.");
            }

            return Success(_mapper.Map<SeoRedirectDto>(redirect), redirect.Id, "SEO redirect retrieved successfully.");
        }

        public async Task<ServiceResponse<SeoRedirectDto>> CreateAsync(UpsertSeoRedirectDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            NormalizeRequest(request);

            var validationResult = await ValidateAsync(request);

            if (!validationResult.Success)
            {
                return validationResult;
            }

            if (await _seoRedirectRepository.OldPathExistsAsync(request.OldPath!))
            {
                return Conflict("Redirect old path is already in use.");
            }

            var redirect = _mapper.Map<SeoRedirect>(request);
            var rowsAffected = await _genericRepository.AddAsync(redirect);

            if (rowsAffected <= 0)
            {
                return Failure("SEO redirect could not be created.");
            }

            return Success(_mapper.Map<SeoRedirectDto>(redirect), redirect.Id, "SEO redirect created successfully.");
        }

        public async Task<ServiceResponse<SeoRedirectDto>> UpdateAsync(Guid id, UpsertSeoRedirectDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (id == Guid.Empty)
            {
                return ValidationError("Redirect id is required.");
            }

            NormalizeRequest(request);

            var validationResult = await ValidateAsync(request);

            if (!validationResult.Success)
            {
                return validationResult;
            }

            var redirect = await _genericRepository.GetByIdAsync(id);

            if (redirect is null)
            {
                return NotFound("Redirect not found.");
            }

            if (await _seoRedirectRepository.OldPathExistsAsync(request.OldPath!, id))
            {
                return Conflict("Redirect old path is already in use.");
            }

            _mapper.Map(request, redirect);
            var rowsAffected = await _genericRepository.UpdateAsync(redirect);

            if (rowsAffected <= 0)
            {
                return Failure("SEO redirect could not be updated.");
            }

            return Success(_mapper.Map<SeoRedirectDto>(redirect), redirect.Id, "SEO redirect updated successfully.");
        }

        public async Task<ServiceResponse<SeoRedirectDto>> DeactivateAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                return ValidationError("Redirect id is required.");
            }

            var redirect = await _genericRepository.GetByIdAsync(id);

            if (redirect is null)
            {
                return NotFound("Redirect not found.");
            }

            redirect.IsActive = false;
            var rowsAffected = await _genericRepository.UpdateAsync(redirect);

            if (rowsAffected <= 0)
            {
                return Failure("SEO redirect could not be deactivated.");
            }

            return Success(_mapper.Map<SeoRedirectDto>(redirect), redirect.Id, "SEO redirect deactivated successfully.");
        }

        public async Task<ServiceResponse<SeoRedirectDto>> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                return ValidationError("Redirect id is required.");
            }

            var redirect = await _genericRepository.GetByIdAsync(id);

            if (redirect is null)
            {
                return NotFound("Redirect not found.");
            }

            var payload = _mapper.Map<SeoRedirectDto>(redirect);
            var rowsAffected = await _genericRepository.DeleteAsync(id);

            if (rowsAffected <= 0)
            {
                return Failure("SEO redirect could not be deleted.");
            }

            return Success(payload, id, "SEO redirect deleted successfully.");
        }

        private async Task<ServiceResponse<SeoRedirectDto>> ValidateAsync(UpsertSeoRedirectDto request)
        {
            var validationDto = _mapper.Map<SeoRedirectDto>(request);
            var validationResult = await _validationService.ValidateAsync(validationDto, _validator);

            if (!validationResult.Success)
            {
                return ValidationError(validationResult.Message ?? "Invalid redirect payload.");
            }

            return new ServiceResponse<SeoRedirectDto>(true)
            {
                ResponseType = ServiceResponseType.Success,
            };
        }

        private static void NormalizeRequest(UpsertSeoRedirectDto request)
        {
            request.OldPath = SeoRedirectPathUtility.NormalizePath(request.OldPath);
            request.NewPath = SeoRedirectPathUtility.NormalizePath(request.NewPath);
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

        private static ServiceResponse<SeoRedirectDto> NotFound(string message)
        {
            return new ServiceResponse<SeoRedirectDto>(false, message)
            {
                ResponseType = ServiceResponseType.NotFound,
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