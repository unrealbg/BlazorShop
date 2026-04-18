namespace BlazorShop.Application.Services.Contracts
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;

    public interface ISeoRedirectService
    {
        Task<IReadOnlyList<SeoRedirectDto>> GetAllAsync();

        Task<ServiceResponse<SeoRedirectDto>> GetByIdAsync(Guid id);

        Task<ServiceResponse<SeoRedirectDto>> CreateAsync(UpsertSeoRedirectDto request);

        Task<ServiceResponse<SeoRedirectDto>> UpdateAsync(Guid id, UpsertSeoRedirectDto request);

        Task<ServiceResponse<SeoRedirectDto>> DeactivateAsync(Guid id);

        Task<ServiceResponse<SeoRedirectDto>> DeleteAsync(Guid id);
    }
}