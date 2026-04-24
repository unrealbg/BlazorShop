namespace BlazorShop.Application.Services
{
    using System.Text.Json;

    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Application.Services.Contracts.Admin;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;

    public class ProductService : IProductService
    {
        private readonly IProductReadRepository _productReadRepository;
        private readonly IGenericRepository<Product> _productRepository;
        private readonly IMapper _mapper;
        private readonly IAdminAuditService? _auditService;

        public ProductService(IProductReadRepository productReadRepository, IGenericRepository<Product> productRepository, IMapper mapper, IAdminAuditService? auditService = null)
        {
            _productReadRepository = productReadRepository;
            _productRepository = productRepository;
            _mapper = mapper;
            _auditService = auditService;
        }

        public async Task<IEnumerable<GetProduct>> GetAllAsync()
        {
            var result = await _productReadRepository.GetCatalogProductsAsync();

            var mappedData = _mapper.Map<IEnumerable<GetProduct>>(result);

            return result.Any() ? mappedData : [];
        }

        public async Task<PagedResult<GetCatalogProduct>> GetCatalogPageAsync(ProductCatalogQuery query)
        {
            var result = await _productReadRepository.GetCatalogPageAsync(query);
            var mappedItems = _mapper.Map<IReadOnlyList<GetCatalogProduct>>(result.Items);

            return new PagedResult<GetCatalogProduct>
            {
                Items = mappedItems,
                PageNumber = result.PageNumber,
                PageSize = result.PageSize,
                TotalCount = result.TotalCount,
            };
        }

        public async Task<GetProduct?> GetByIdAsync(Guid id)
        {
            var result = await _productReadRepository.GetProductDetailsByIdAsync(id);
            return result != null ? _mapper.Map<GetProduct>(result) : null;
        }

        public async Task<ServiceResponse> AddAsync(CreateProduct product)
        {
            var mappedData = _mapper.Map<Product>(product);
            int result = await _productRepository.AddAsync(mappedData);

            if (result <= 0)
            {
                return new ServiceResponse(false, "Product not added");
            }

            await LogAsync("Product.Created", mappedData.Id, $"Product {mappedData.Name} created.", new { mappedData.Name, mappedData.Price, mappedData.Quantity });
            return new ServiceResponse(true, "Product added successfully", mappedData.Id);
        }

        public async Task<ServiceResponse> UpdateAsync(UpdateProduct product)
        {
            var existingProduct = await _productRepository.GetByIdAsync(product.Id);

            if (existingProduct is null)
            {
                return new ServiceResponse(false, "Product not found");
            }

            _mapper.Map(product, existingProduct);
            int result = await _productRepository.UpdateAsync(existingProduct);

            if (result <= 0)
            {
                return new ServiceResponse(false, "Product not found");
            }

            await LogAsync("Product.Updated", existingProduct.Id, $"Product {existingProduct.Name} updated.", new { existingProduct.Name, existingProduct.Price, existingProduct.Quantity });
            return new ServiceResponse(true, "Product updated successfully");
        }

        public async Task<ServiceResponse> DeleteAsync(Guid id)
        {
            var existingProduct = await _productRepository.GetByIdAsync(id);
            var result = await _productRepository.DeleteAsync(id);

            if (result <= 0)
            {
                return new ServiceResponse(false, "Product not found");
            }

            await LogAsync("Product.Deleted", id, $"Product {existingProduct?.Name ?? id.ToString()} deleted.", new { existingProduct?.Name });
            return new ServiceResponse(true, "Product deleted successfully");
        }

        private async Task LogAsync(string action, Guid entityId, string summary, object metadata)
        {
            if (_auditService is null)
            {
                return;
            }

            await _auditService.LogAsync(new CreateAdminAuditLogDto
            {
                Action = action,
                EntityType = "Product",
                EntityId = entityId.ToString(),
                Summary = summary,
                MetadataJson = JsonSerializer.Serialize(metadata),
            });
        }
    }
}
