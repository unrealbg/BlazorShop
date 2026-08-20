namespace BlazorShop.Infrastructure.Repositories
{
    using System.Data;

    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    public sealed class ProductInventoryTopologyRepository : IProductInventoryTopologyRepository
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public ProductInventoryTopologyRepository(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task<ProductInventoryTopologyResult> AddVariantAsync(
            ProductVariant variant,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(variant);

            var commitMayHaveSucceeded = false;
            await using var strategyContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var strategy = strategyContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
                if (commitMayHaveSucceeded)
                {
                    var existingVariant = await db.ProductVariants
                        .AsNoTracking()
                        .SingleOrDefaultAsync(item => item.Id == variant.Id, cancellationToken);
                    if (existingVariant is not null)
                    {
                        return existingVariant.ProductId == variant.ProductId
                            ? new ProductInventoryTopologyResult(true, "Variant added successfully")
                            : new ProductInventoryTopologyResult(false, "Variant identifier is already in use.");
                    }

                    commitMayHaveSucceeded = false;
                }

                await using var transaction = await db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

                var product = (await db.Products
                    .FromSqlInterpolated($"SELECT * FROM \"Products\" WHERE \"Id\" = {variant.ProductId} FOR UPDATE")
                    .ToListAsync(cancellationToken))
                    .SingleOrDefault();
                if (product is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new ProductInventoryTopologyResult(false, "Product not found");
                }

                var hasVariants = await db.ProductVariants
                    .AsNoTracking()
                    .AnyAsync(item => item.ProductId == product.Id, cancellationToken);
                if (!hasVariants)
                {
                    var hasActiveProductReservation = await db.InventoryReservations
                        .AsNoTracking()
                        .AnyAsync(
                            item => item.ProductId == product.Id
                                && item.ProductVariantId == null
                                && item.Status == InventoryReservationStatus.Reserved,
                            cancellationToken);
                    if (hasActiveProductReservation)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return new ProductInventoryTopologyResult(
                            false,
                            "The first variant cannot be added while product inventory is reserved.",
                            product.Name);
                    }
                }

                // Product quantity is never sellable while variants exist.
                product.Quantity = 0;
                variant.Stock = 0;
                db.ProductVariants.Add(variant);
                await db.SaveChangesAsync(cancellationToken);

                commitMayHaveSucceeded = true;
                await transaction.CommitAsync(cancellationToken);
                commitMayHaveSucceeded = false;
                return new ProductInventoryTopologyResult(true, "Variant added successfully", product.Name);
            });
        }

        public async Task<ProductInventoryTopologyResult> DeleteVariantAsync(
            Guid variantId,
            CancellationToken cancellationToken = default)
        {
            var commitMayHaveSucceeded = false;
            string? deletedProductName = null;
            await using var strategyContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var strategy = strategyContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
                var variantProductId = await db.ProductVariants
                    .AsNoTracking()
                    .Where(item => item.Id == variantId)
                    .Select(item => (Guid?)item.ProductId)
                    .SingleOrDefaultAsync(cancellationToken);
                if (!variantProductId.HasValue)
                {
                    return commitMayHaveSucceeded
                        ? new ProductInventoryTopologyResult(true, "Variant deleted successfully", deletedProductName)
                        : new ProductInventoryTopologyResult(false, "Variant not found");
                }

                commitMayHaveSucceeded = false;
                await using var transaction = await db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

                var product = (await db.Products
                    .FromSqlInterpolated($"SELECT * FROM \"Products\" WHERE \"Id\" = {variantProductId.Value} FOR UPDATE")
                    .ToListAsync(cancellationToken))
                    .SingleOrDefault();
                if (product is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new ProductInventoryTopologyResult(false, "Product not found");
                }

                var selectedVariant = (await db.ProductVariants
                    .FromSqlInterpolated($"SELECT * FROM \"ProductVariants\" WHERE \"Id\" = {variantId} FOR UPDATE")
                    .ToListAsync(cancellationToken))
                    .SingleOrDefault();
                if (selectedVariant is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new ProductInventoryTopologyResult(false, "Variant not found");
                }

                var hasActiveReservation = await db.InventoryReservations
                    .AsNoTracking()
                    .AnyAsync(
                        item => item.ProductVariantId == variantId
                            && item.Status == InventoryReservationStatus.Reserved,
                        cancellationToken);
                if (hasActiveReservation)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new ProductInventoryTopologyResult(
                        false,
                        "A variant with reserved inventory cannot be deleted.",
                        product.Name);
                }

                // This also prevents legacy product quantity from becoming sellable when the last variant is removed.
                product.Quantity = 0;
                db.ProductVariants.Remove(selectedVariant);
                await db.SaveChangesAsync(cancellationToken);

                deletedProductName = product.Name;
                commitMayHaveSucceeded = true;
                await transaction.CommitAsync(cancellationToken);
                commitMayHaveSucceeded = false;
                return new ProductInventoryTopologyResult(true, "Variant deleted successfully", product.Name);
            });
        }

        public async Task<ProductInventoryTopologyResult> DeleteProductAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            var commitMayHaveSucceeded = false;
            string? deletedProductName = null;
            await using var strategyContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var strategy = strategyContext.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(async () =>
            {
                await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
                await using var transaction = await db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

                var product = (await db.Products
                    .FromSqlInterpolated($"SELECT * FROM \"Products\" WHERE \"Id\" = {productId} FOR UPDATE")
                    .ToListAsync(cancellationToken))
                    .SingleOrDefault();
                if (product is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return commitMayHaveSucceeded
                        ? new ProductInventoryTopologyResult(true, "Product deleted successfully", deletedProductName)
                        : new ProductInventoryTopologyResult(false, "Product not found");
                }

                commitMayHaveSucceeded = false;
                var hasActiveReservation = await db.InventoryReservations
                    .AsNoTracking()
                    .AnyAsync(
                        item => item.ProductId == productId
                            && item.Status == InventoryReservationStatus.Reserved,
                        cancellationToken);
                if (hasActiveReservation)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new ProductInventoryTopologyResult(
                        false,
                        "A product with reserved inventory cannot be deleted.",
                        product.Name);
                }

                deletedProductName = product.Name;
                db.Products.Remove(product);
                await db.SaveChangesAsync(cancellationToken);

                commitMayHaveSucceeded = true;
                await transaction.CommitAsync(cancellationToken);
                commitMayHaveSucceeded = false;
                return new ProductInventoryTopologyResult(true, "Product deleted successfully", deletedProductName);
            });
        }
    }
}
