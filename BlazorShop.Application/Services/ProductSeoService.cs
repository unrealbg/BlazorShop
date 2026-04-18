namespace BlazorShop.Application.Services
{
    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;
    using BlazorShop.Application.Services.Contracts;
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
        private readonly IValidationService _validationService;
        private readonly IValidator<UpdateProductSeoDto> _validator;

        public ProductSeoService(
            IGenericRepository<Product> productRepository,
            IProductReadRepository productReadRepository,
            IMapper mapper,
            ISlugService slugService,
            IValidationService validationService,
            IValidator<UpdateProductSeoDto> validator)
        {
            _productRepository = productRepository;
            _productReadRepository = productReadRepository;
            _mapper = mapper;
            _slugService = slugService;
            _validationService = validationService;
            _validator = validator;
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

            var existingPublishedOn = product.PublishedOn;
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
                return Failure("Product SEO update failed.");
            }

            return Success(_mapper.Map<ProductSeoDto>(product), product.Id, "Product SEO updated successfully.");
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