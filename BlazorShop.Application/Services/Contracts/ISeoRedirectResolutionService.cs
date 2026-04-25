namespace BlazorShop.Application.Services.Contracts
{
    using BlazorShop.Application.DTOs.Seo;

    public interface ISeoRedirectResolutionService
    {
        Task<SeoRedirectResolutionDto?> ResolvePublicPathAsync(string? path);
    }
}