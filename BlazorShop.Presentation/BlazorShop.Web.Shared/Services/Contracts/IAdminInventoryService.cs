namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Admin.Inventory;

    public interface IAdminInventoryService
    {
        Task<QueryResult<PagedResult<AdminInventoryItem>>> GetAsync(AdminInventoryQuery query);

        Task<ServiceResponse<AdminInventoryItem>> UpdateProductStockAsync(Guid productId, UpdateProductStock request);

        Task<ServiceResponse<AdminInventoryVariant>> UpdateVariantStockAsync(Guid variantId, UpdateVariantStock request);
    }
}
