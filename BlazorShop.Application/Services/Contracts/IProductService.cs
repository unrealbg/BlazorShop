namespace BlazorShop.Application.Services.Contracts
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Product;
    using BlazorShop.Domain.Contracts;

    public interface IProductService
    {
        Task<IEnumerable<GetProduct>> GetAllAsync();

        Task<PagedResult<GetCatalogProduct>> GetCatalogPageAsync(ProductCatalogQuery query);

        Task<GetProduct?> GetByIdAsync(Guid id);

        Task<ServiceResponse> AddAsync(CreateProduct product);

        Task<ServiceResponse> UpdateAsync(UpdateProduct product);

        Task<ServiceResponse> DeleteAsync(Guid id);
    }
}
