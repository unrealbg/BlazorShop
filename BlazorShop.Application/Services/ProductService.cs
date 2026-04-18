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
        private readonly IProductReadRepository _productReadRepository;
        private readonly IGenericRepository<Product> _productRepository;
        private readonly IMapper _mapper;

        public ProductService(IProductReadRepository productReadRepository, IGenericRepository<Product> productRepository, IMapper mapper)
        {
            _productReadRepository = productReadRepository;
            _productRepository = productRepository;
            _mapper = mapper;
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

            return result > 0
                ? new ServiceResponse(true, "Product added successfully", mappedData.Id)
                : new ServiceResponse(false, "Product not added");
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

            return result > 0 ? new ServiceResponse(true, "Product updated successfully") : new ServiceResponse(false, "Product not found");
        }

        public async Task<ServiceResponse> DeleteAsync(Guid id)
        {
            var result = await _productRepository.DeleteAsync(id);

            return result > 0 ? new ServiceResponse(true, "Product deleted successfully") : new ServiceResponse(false, "Product not found");
        }
    }
}
