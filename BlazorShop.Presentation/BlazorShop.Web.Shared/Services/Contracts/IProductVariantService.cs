namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Product;

    public interface IProductVariantService
    {
        Task<IEnumerable<GetProductVariant>> GetByProductIdAsync(Guid productId);

        Task<ServiceResponse> AddAsync(Guid productId, CreateOrUpdateProductVariant variant);

        Task<ServiceResponse> UpdateAsync(CreateOrUpdateProductVariant variant);

        Task<ServiceResponse> DeleteAsync(Guid variantId);
    }
}
