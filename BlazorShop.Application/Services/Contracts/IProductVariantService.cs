namespace BlazorShop.Application.Services.Contracts
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Product.ProductVariant;

    public interface IProductVariantService
    {
        Task<IEnumerable<GetProductVariant>> GetByProductIdAsync(Guid productId);

        Task<ServiceResponse> AddAsync(CreateProductVariant variant);

        Task<ServiceResponse> UpdateAsync(UpdateProductVariant variant);

        Task<ServiceResponse> DeleteAsync(Guid variantId);
    }
}
