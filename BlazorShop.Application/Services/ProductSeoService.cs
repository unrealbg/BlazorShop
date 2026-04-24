namespace BlazorShop.Application.Services
{
    using System.Text.Json;

    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Exceptions;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Application.Services.Contracts.Admin;
    using BlazorShop.Application.Validations;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;

    using FluentValidation;

    public class ProductSeoService : IProductSeoService
    {
        private readonly IGenericRepository<Product> _productRepository;
        private readonly IProductReadRepository _productReadRepository;
        private readonly IMapper _mapper;
        private readonly ISlugService _slugService;
        private readonly IApplicationTransactionManager _transactionManager;
        private readonly ISeoRedirectAutomationService _seoRedirectAutomationService;
        private readonly IValidationService _validationService;
        private readonly IValidator<UpdateProductSeoDto> _validator;
        private readonly IAdminAuditService? _auditService;

        public ProductSeoService(
            IGenericRepository<Product> productRepository,
            IProductReadRepository productReadRepository,
            IMapper mapper,
            ISlugService slugService,
            IApplicationTransactionManager transactionManager,
            ISeoRedirectAutomationService seoRedirectAutomationService,
            IValidationService validationService,
            IValidator<UpdateProductSeoDto> validator,
            IAdminAuditService? auditService = null)
        {
            _productRepository = productRepository;
            _productReadRepository = productReadRepository;
            _mapper = mapper;
            _slugService = slugService;
            _transactionManager = transactionManager;
            _seoRedirectAutomationService = seoRedirectAutomationService;
            _validationService = validationService;
            _validator = validator;
            _auditService = auditService;
        }

        public async Task<ServiceResponse<ProductSeoDto>> GetByProductIdAsync(Guid productId)
        {
            if (productId == Guid.Empty)
            {
                return ValidationError("Product id is required.");
            }

            var product = await _productRepository.GetByIdAsync(productId);

            if (product is null)
            {
                return NotFound("Product not found.");
            }

            return Success(_mapper.Map<ProductSeoDto>(product), product.Id, "Product SEO retrieved successfully.");
        }

        public async Task<ServiceResponse<ProductSeoDto>> UpdateAsync(Guid productId, UpdateProductSeoDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (productId == Guid.Empty)
            {
                return ValidationError("Product id is required.");
            }

            var normalizedRequest = CopyRequest(productId, request);
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

            var product = await _productRepository.GetByIdAsync(productId);

            if (product is null)
            {
                return NotFound("Product not found.");
            }

            if (!string.IsNullOrWhiteSpace(normalizedRequest.Slug)
                && await _productReadRepository.ProductSlugExistsAsync(normalizedRequest.Slug, productId))
            {
                return Conflict("Product slug is already in use.");
            }

            var productSnapshot = await _productReadRepository.GetProductDetailsByIdAsync(productId);
            var existingPublishedOn = product.PublishedOn;
            var oldPublicPath = BuildProductPublicPath(productSnapshot?.Slug, productSnapshot?.IsPublished == true, productSnapshot?.PublishedOn, productSnapshot?.Category?.IsPublished == true);
            var newPublicPath = BuildProductPublicPath(normalizedRequest.Slug, normalizedRequest.IsPublished, normalizedRequest.PublishedOn ?? existingPublishedOn ?? DateTime.UtcNow, productSnapshot?.Category?.IsPublished == true);

            try
            {
                return await _transactionManager.ExecuteInTransactionAsync(async () =>
                {
                    await EnsureRedirectAsync(oldPublicPath, newPublicPath);

                    _mapper.Map(normalizedRequest, product);

                    if (product.IsPublished)
                    {
                        product.PublishedOn = normalizedRequest.PublishedOn ?? existingPublishedOn ?? DateTime.UtcNow;
                    }
                    else
                    {
                        product.PublishedOn = null;
                    }

                    var rowsAffected = await _productRepository.UpdateAsync(product);

                    if (rowsAffected <= 0)
                    {
                        throw new ServiceResponseException("Product SEO update failed.", ServiceResponseType.Failure);
                    }

                    await LogAsync(product.Id, "Product SEO updated.", normalizedRequest);
                    return Success(_mapper.Map<ProductSeoDto>(product), product.Id, "Product SEO updated successfully.");
                });
            }
            catch (ServiceResponseException exception)
            {
                return FromServiceException(exception);
            }
        }

        private string? NormalizeSlug(UpdateProductSeoDto request)
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

        private static UpdateProductSeoDto CopyRequest(Guid productId, UpdateProductSeoDto request)
        {
            return new UpdateProductSeoDto
            {
                ProductId = productId,
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
                PublishedOn = request.PublishedOn,
            };
        }

        private async Task LogAsync(Guid productId, string summary, UpdateProductSeoDto request)
        {
            if (_auditService is null)
            {
                return;
            }

            await _auditService.LogAsync(new CreateAdminAuditLogDto
            {
                Action = "ProductSeo.Updated",
                EntityType = "Product",
                EntityId = productId.ToString(),
                Summary = summary,
                MetadataJson = JsonSerializer.Serialize(new { request.Slug, request.MetaTitle, request.IsPublished }),
            });
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

        private static string? BuildProductPublicPath(string? slug, bool isPublished, DateTime? publishedOn, bool isCategoryPublished)
        {
            return isPublished && publishedOn.HasValue && isCategoryPublished && !string.IsNullOrWhiteSpace(slug)
                ? $"/product/{slug}"
                : null;
        }

        private static ServiceResponse<ProductSeoDto> FromServiceException(ServiceResponseException exception)
        {
            return new ServiceResponse<ProductSeoDto>(false, exception.Message)
            {
                ResponseType = exception.ResponseType,
            };
        }

        private static ServiceResponse<ProductSeoDto> Success(ProductSeoDto payload, Guid id, string message)
        {
            return new ServiceResponse<ProductSeoDto>(true, message, id)
            {
                Payload = payload,
                ResponseType = ServiceResponseType.Success,
            };
        }

        private static ServiceResponse<ProductSeoDto> ValidationError(string message)
        {
            return new ServiceResponse<ProductSeoDto>(false, message)
            {
                ResponseType = ServiceResponseType.ValidationError,
            };
        }

        private static ServiceResponse<ProductSeoDto> NotFound(string message)
        {
            return new ServiceResponse<ProductSeoDto>(false, message)
            {
                ResponseType = ServiceResponseType.NotFound,
            };
        }

        private static ServiceResponse<ProductSeoDto> Conflict(string message)
        {
            return new ServiceResponse<ProductSeoDto>(false, message)
            {
                ResponseType = ServiceResponseType.Conflict,
            };
        }

        private static ServiceResponse<ProductSeoDto> Failure(string message)
        {
            return new ServiceResponse<ProductSeoDto>(false, message)
            {
                ResponseType = ServiceResponseType.Failure,
            };
        }
    }
}
