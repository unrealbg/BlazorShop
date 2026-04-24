namespace BlazorShop.Application.Services.Contracts.Admin
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Inventory;
    using BlazorShop.Domain.Contracts;

    public interface IAdminInventoryService
    {
        Task<PagedResult<AdminInventoryItemDto>> GetAsync(AdminInventoryQueryDto query);

        Task<ServiceResponse<AdminInventoryItemDto>> UpdateProductStockAsync(Guid productId, UpdateProductStockDto request);

        Task<ServiceResponse<AdminInventoryVariantDto>> UpdateVariantStockAsync(Guid variantId, UpdateVariantStockDto request);
    }
}
