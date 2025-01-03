namespace BlazorShop.Application.Services
{
    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;

    public class ProductService : IProductService
    {
        private readonly IGenericRepository<Product> _productRepository;
        private readonly IMapper _mapper;

        public ProductService(IGenericRepository<Product> productRepository, IMapper mapper)
        {
            _productRepository = productRepository;
            _mapper = mapper;
        }

        public async Task<IEnumerable<GetProduct>> GetAllAsync()
        {
            var result = await _productRepository.GetAllAsync();

            var mappedData = _mapper.Map<IEnumerable<GetProduct>>(result);

            return result.Any() ? mappedData : [];
        }

        public async Task<GetProduct> GetByIdAsync(Guid id)
        {
            var result = await _productRepository.GetByIdAsync(id);
            return result != null ? _mapper.Map<GetProduct>(result) : null;
        }

        public async Task<ServiceResponse> AddAsync(CreateProduct product)
        {
            var mappedData = _mapper.Map<Product>(product);
            int result = await _productRepository.AddAsync(mappedData);

            return result > 0 ? new ServiceResponse(true, "Product added successfully") : new ServiceResponse(false, "Product not added");
        }

        public async Task<ServiceResponse> UpdateAsync(UpdateProduct product)
        {
            var mappedData = _mapper.Map<Product>(product);
            int result = await _productRepository.UpdateAsync(mappedData);

            return result > 0 ? new ServiceResponse(true, "Product updated successfully") : new ServiceResponse(false, "Product not found");
        }

        public async Task<ServiceResponse> DeleteAsync(Guid id)
        {
            var result = await _productRepository.DeleteAsync(id);

            return result > 0 ? new ServiceResponse(true, "Product deleted successfully") : new ServiceResponse(false, "Product not found");
        }
    }
}
