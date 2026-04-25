namespace BlazorShop.Application.Services.Contracts
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;

    public interface ISeoSettingsService
    {
        Task<SeoSettingsDto> GetCurrentAsync();

        Task<ServiceResponse<SeoSettingsDto>> UpdateAsync(UpdateSeoSettingsDto request);
    }
}