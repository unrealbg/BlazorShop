namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Product;

    public interface IProductService
    {
        Task<IEnumerable<GetProduct>> GetAllAsync();

        Task<GetProduct> GetByIdAsync(Guid id);

        Task<ServiceResponse> AddAsync(CreateProduct product);

        Task<ServiceResponse> UpdateAsync(UpdateProduct product);

        Task<ServiceResponse> DeleteAsync(Guid id);
    }
}
