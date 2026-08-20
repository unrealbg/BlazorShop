namespace BlazorShop.Domain.Contracts
{
    using BlazorShop.Domain.Entities;

    public interface IProductInventoryTopologyRepository
    {
        Task<ProductInventoryTopologyResult> AddVariantAsync(
            ProductVariant variant,
            CancellationToken cancellationToken = default);

        Task<ProductInventoryTopologyResult> DeleteVariantAsync(
            Guid variantId,
            CancellationToken cancellationToken = default);

        Task<ProductInventoryTopologyResult> DeleteProductAsync(
            Guid productId,
            CancellationToken cancellationToken = default);
    }

    public sealed record ProductInventoryTopologyResult(
        bool Success,
        string Message,
        string? ProductName = null);
}
