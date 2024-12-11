namespace BlazorShop.Application.Services.Contracts
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Product;

    public interface IProductService
    {
        Task<IEnumerable<CreateProduct>> GetAllAsync();

        Task<GetProduct> GetByIdAsync(Guid id);

        Task<ServiceResponse> AddAsync(CreateProduct product);

        Task<ServiceResponse> UpdateAsync(UpdateProduct product);

        Task<ServiceResponse> DeleteAsync(Guid id);
    }
}
