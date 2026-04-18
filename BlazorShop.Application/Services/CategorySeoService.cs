namespace BlazorShop.Application.Services
{
    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Exceptions;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Application.Validations;
    using BlazorShop.Domain.Contracts.CategoryPersistence;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;

    using FluentValidation;

    public class CategorySeoService : ICategorySeoService
    {
        private readonly IGenericRepository<Category> _categoryRepository;
        private readonly ICategoryRepository _categoryReadRepository;
        private readonly IMapper _mapper;
        private readonly ISlugService _slugService;
        private readonly IApplicationTransactionManager _transactionManager;
        private readonly ISeoRedirectAutomationService _seoRedirectAutomationService;
        private readonly IValidationService _validationService;
        private readonly IValidator<UpdateCategorySeoDto> _validator;

        public CategorySeoService(
            IGenericRepository<Category> categoryRepository,
            ICategoryRepository categoryReadRepository,
            IMapper mapper,
            ISlugService slugService,
            IApplicationTransactionManager transactionManager,
            ISeoRedirectAutomationService seoRedirectAutomationService,
            IValidationService validationService,
            IValidator<UpdateCategorySeoDto> validator)
        {
            _categoryRepository = categoryRepository;
            _categoryReadRepository = categoryReadRepository;
            _mapper = mapper;
            _slugService = slugService;
            _transactionManager = transactionManager;
            _seoRedirectAutomationService = seoRedirectAutomationService;
            _validationService = validationService;
            _validator = validator;
        }

        public async Task<ServiceResponse<CategorySeoDto>> GetByCategoryIdAsync(Guid categoryId)
        {
            if (categoryId == Guid.Empty)
            {
                return ValidationError("Category id is required.");
            }

            var category = await _categoryRepository.GetByIdAsync(categoryId);

            if (category is null)
            {
                return NotFound("Category not found.");
            }

            return Success(_mapper.Map<CategorySeoDto>(category), category.Id, "Category SEO retrieved successfully.");
        }

        public async Task<ServiceResponse<CategorySeoDto>> UpdateAsync(Guid categoryId, UpdateCategorySeoDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (categoryId == Guid.Empty)
            {
                return ValidationError("Category id is required.");
            }

            var normalizedRequest = CopyRequest(categoryId, request);
            var slugValidationMessage = NormalizeSlug(normalizedRequest);

            if (slugValidationMessage is not null)
            {
                return ValidationError(slugValidationMessage);
            }

            var validationResult = await _validationService.ValidateAsync(normalizedRequest, _validator);

            if (!validationResult.Success)
            {
                return ValidationError(validationResult.Message ?? "Invalid SEO payload.");
            }

            var category = await _categoryRepository.GetByIdAsync(categoryId);

            if (category is null)
            {
                return NotFound("Category not found.");
            }

            if (!string.IsNullOrWhiteSpace(normalizedRequest.Slug)
                && await _categoryReadRepository.CategorySlugExistsAsync(normalizedRequest.Slug, categoryId))
            {
                return Conflict("Category slug is already in use.");
            }

            var oldPublicPath = BuildCategoryPublicPath(category.Slug, category.IsPublished);
            var newPublicPath = BuildCategoryPublicPath(normalizedRequest.Slug, normalizedRequest.IsPublished);

            try
            {
                return await _transactionManager.ExecuteInTransactionAsync(async () =>
                {
                    await EnsureRedirectAsync(oldPublicPath, newPublicPath);

                    _mapper.Map(normalizedRequest, category);
                    var rowsAffected = await _categoryRepository.UpdateAsync(category);

                    if (rowsAffected <= 0)
                    {
                        throw new ServiceResponseException("Category SEO update failed.", ServiceResponseType.Failure);
                    }

                    return Success(_mapper.Map<CategorySeoDto>(category), category.Id, "Category SEO updated successfully.");
                });
            }
            catch (ServiceResponseException exception)
            {
                return FromServiceException(exception);
            }
        }

        private string? NormalizeSlug(UpdateCategorySeoDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Slug))
            {
                request.Slug = null;
                return null;
            }

            var normalizedSlug = _slugService.NormalizeSlug(request.Slug);

            if (string.IsNullOrWhiteSpace(normalizedSlug))
            {
                return "Slug is invalid after normalization.";
            }

            request.Slug = normalizedSlug;
            return null;
        }

        private static UpdateCategorySeoDto CopyRequest(Guid categoryId, UpdateCategorySeoDto request)
        {
            return new UpdateCategorySeoDto
            {
                CategoryId = categoryId,
                Slug = request.Slug,
                MetaTitle = request.MetaTitle,
                MetaDescription = request.MetaDescription,
                CanonicalUrl = request.CanonicalUrl,
                OgTitle = request.OgTitle,
                OgDescription = request.OgDescription,
                OgImage = request.OgImage,
                RobotsIndex = request.RobotsIndex,
                RobotsFollow = request.RobotsFollow,
                SeoContent = request.SeoContent,
                IsPublished = request.IsPublished,
            };
        }

        private async Task EnsureRedirectAsync(string? oldPublicPath, string? newPublicPath)
        {
            if (string.IsNullOrWhiteSpace(oldPublicPath)
                || string.IsNullOrWhiteSpace(newPublicPath)
                || SeoRedirectPathUtility.PathsEqual(oldPublicPath, newPublicPath))
            {
                return;
            }

            var redirectResult = await _seoRedirectAutomationService.EnsurePermanentRedirectAsync(oldPublicPath, newPublicPath);
            if (!redirectResult.Success)
            {
                throw new ServiceResponseException(
                    redirectResult.Message ?? "Automatic redirect could not be created.",
                    redirectResult.ResponseType);
            }
        }

        private static string? BuildCategoryPublicPath(string? slug, bool isPublished)
        {
            return isPublished && !string.IsNullOrWhiteSpace(slug)
                ? $"/category/{slug}"
                : null;
        }

        private static ServiceResponse<CategorySeoDto> FromServiceException(ServiceResponseException exception)
        {
            return new ServiceResponse<CategorySeoDto>(false, exception.Message)
            {
                ResponseType = exception.ResponseType,
            };
        }

        private static ServiceResponse<CategorySeoDto> Success(CategorySeoDto payload, Guid id, string message)
        {
            return new ServiceResponse<CategorySeoDto>(true, message, id)
            {
                Payload = payload,
                ResponseType = ServiceResponseType.Success,
            };
        }

        private static ServiceResponse<CategorySeoDto> ValidationError(string message)
        {
            return new ServiceResponse<CategorySeoDto>(false, message)
            {
                ResponseType = ServiceResponseType.ValidationError,
            };
        }

        private static ServiceResponse<CategorySeoDto> NotFound(string message)
        {
            return new ServiceResponse<CategorySeoDto>(false, message)
            {
                ResponseType = ServiceResponseType.NotFound,
            };
        }

        private static ServiceResponse<CategorySeoDto> Conflict(string message)
        {
            return new ServiceResponse<CategorySeoDto>(false, message)
            {
                ResponseType = ServiceResponseType.Conflict,
            };
        }

        private static ServiceResponse<CategorySeoDto> Failure(string message)
        {
            return new ServiceResponse<CategorySeoDto>(false, message)
            {
                ResponseType = ServiceResponseType.Failure,
            };
        }
    }
}