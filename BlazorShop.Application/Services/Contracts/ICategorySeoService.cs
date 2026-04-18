namespace BlazorShop.Application.Services.Contracts
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;

    public interface ICategorySeoService
    {
        Task<ServiceResponse<CategorySeoDto>> GetByCategoryIdAsync(Guid categoryId);

        Task<ServiceResponse<CategorySeoDto>> UpdateAsync(Guid categoryId, UpdateCategorySeoDto request);
    }
}