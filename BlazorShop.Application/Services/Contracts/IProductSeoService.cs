namespace BlazorShop.Application.Services.Contracts
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Seo;

    public interface IProductSeoService
    {
        Task<ServiceResponse<ProductSeoDto>> GetByProductIdAsync(Guid productId);

        Task<ServiceResponse<ProductSeoDto>> UpdateAsync(Guid productId, UpdateProductSeoDto request);
    }
}