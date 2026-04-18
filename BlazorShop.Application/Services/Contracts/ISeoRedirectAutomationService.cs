namespace BlazorShop.Application.Services.Contracts
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;

    public interface ISeoRedirectAutomationService
    {
        Task<ServiceResponse<SeoRedirectDto>> EnsurePermanentRedirectAsync(string oldPath, string newPath);
    }
}