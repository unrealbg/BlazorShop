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
        private readonly IMapper _mapper;

        public ProductVariantService(IGenericRepository<ProductVariant> variantRepository, IMapper mapper)
        {
            _variantRepository = variantRepository;
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
            var result = await _variantRepository.AddAsync(mapped);
            return result > 0 ? new ServiceResponse(true, "Variant added successfully") : new ServiceResponse(false, "Variant not added");
        }

        public async Task<ServiceResponse> UpdateAsync(UpdateProductVariant variant)
        {
            var mapped = _mapper.Map<ProductVariant>(variant);
            var result = await _variantRepository.UpdateAsync(mapped);
            return result > 0 ? new ServiceResponse(true, "Variant updated successfully") : new ServiceResponse(false, "Variant not found");
        }

        public async Task<ServiceResponse> DeleteAsync(Guid variantId)
        {
            var result = await _variantRepository.DeleteAsync(variantId);
            return result > 0 ? new ServiceResponse(true, "Variant deleted successfully") : new ServiceResponse(false, "Variant not found");
        }
    }
}
