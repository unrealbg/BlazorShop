namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Product;

    public interface ICategoryService
    {
        Task<QueryResult<IEnumerable<GetCategory>>> GetAllAsync();

        Task<QueryResult<IEnumerable<GetCategory>>> GetAllForAdminAsync();

        Task<QueryResult<GetCategory>> GetByIdAsync(Guid id);

        Task<ServiceResponse> AddAsync(CreateCategory category);

        Task<ServiceResponse> UpdateAsync(UpdateCategory category);

        Task<ServiceResponse> DeleteAsync(Guid id);

        Task<QueryResult<IEnumerable<GetProduct>>> GetProductsByCategoryAsync(Guid id);
    }
}
