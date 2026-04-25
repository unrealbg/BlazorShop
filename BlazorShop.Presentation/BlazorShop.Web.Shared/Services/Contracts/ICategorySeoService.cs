namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Seo;

    public interface ICategorySeoService
    {
        Task<QueryResult<GetCategorySeo>> GetByCategoryIdAsync(Guid categoryId);

        Task<ServiceResponse<GetCategorySeo>> UpdateAsync(Guid categoryId, UpdateCategorySeo request);
    }
}