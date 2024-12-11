namespace BlazorShop.Application.Services.Contracts
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Category;

    public interface ICategoryService
    {
        Task<IEnumerable<GetCategory>> GetAllAsync();

        Task<GetCategory> GetByIdAsync(Guid id);

        Task<ServiceResponse> AddAsync(CreateCategory category);

        Task<ServiceResponse> UpdateAsync(UpdateCategory category);

        Task<ServiceResponse> DeleteAsync(Guid id);
    }
}
