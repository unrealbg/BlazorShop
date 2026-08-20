namespace BlazorShop.Application.Services
{
    using AutoMapper;
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Product.ProductVariant;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;

    public class ProductVariantService : IProductVariantService
    {
        private readonly IGenericRepository<ProductVariant> _variantRepository;
        private readonly IProductInventoryTopologyRepository _inventoryTopologyRepository;
        private readonly IMapper _mapper;

        public ProductVariantService(
            IGenericRepository<ProductVariant> variantRepository,
            IProductInventoryTopologyRepository inventoryTopologyRepository,
            IMapper mapper)
        {
            _variantRepository = variantRepository;
            _inventoryTopologyRepository = inventoryTopologyRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<GetProductVariant>> GetByProductIdAsync(Guid productId)
        {
            var all = await _variantRepository.GetAllAsync();
            var data = all.Where(v => v.ProductId == productId);
            return data.Any() ? _mapper.Map<IEnumerable<GetProductVariant>>(data) : Array.Empty<GetProductVariant>();
        }

        public async Task<ServiceResponse> AddAsync(CreateProductVariant variant)
        {
            var mapped = _mapper.Map<ProductVariant>(variant);
            mapped.Id = mapped.Id == Guid.Empty ? Guid.NewGuid() : mapped.Id;
            mapped.Stock = 0;
            var result = await _inventoryTopologyRepository.AddVariantAsync(mapped);
            return new ServiceResponse(result.Success, result.Message);
        }

        public async Task<ServiceResponse> UpdateAsync(UpdateProductVariant variant)
        {
            var existingVariant = await _variantRepository.GetByIdAsync(variant.Id);
            if (existingVariant is null)
            {
                return new ServiceResponse(false, "Variant not found");
            }

            if (variant.ProductId != existingVariant.ProductId)
            {
                return new ServiceResponse(false, "A product variant cannot be moved to another product");
            }

            var currentStock = existingVariant.Stock;
            _mapper.Map(variant, existingVariant);
            existingVariant.Stock = currentStock;
            var result = await _variantRepository.UpdateAsync(existingVariant);
            return result > 0 ? new ServiceResponse(true, "Variant updated successfully") : new ServiceResponse(false, "Variant not found");
        }

        public async Task<ServiceResponse> DeleteAsync(Guid variantId)
        {
            var result = await _inventoryTopologyRepository.DeleteVariantAsync(variantId);
            return new ServiceResponse(result.Success, result.Message);
        }
    }
}
