namespace BlazorShop.Infrastructure.Services.Admin
{
    using System.Text.Json;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Admin.Audit;
    using BlazorShop.Application.DTOs.Admin.Inventory;
    using BlazorShop.Application.Services.Contracts.Admin;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    public class AdminInventoryService : IAdminInventoryService
    {
        private readonly AppDbContext _db;
        private readonly IAdminAuditService _auditService;

        public AdminInventoryService(AppDbContext db, IAdminAuditService auditService)
        {
            _db = db;
            _auditService = auditService;
        }

        public async Task<PagedResult<AdminInventoryItemDto>> GetAsync(AdminInventoryQueryDto query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var pageNumber = Math.Max(1, query.PageNumber);
            var pageSize = Math.Clamp(query.PageSize, 1, 100);
            var threshold = Math.Max(0, query.LowStockThreshold);
            var products = _db.Products
                .AsNoTracking()
                .Include(product => product.Category)
                .Include(product => product.Variants)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.SearchTerm))
            {
                var search = query.SearchTerm.Trim().ToLowerInvariant();
                products = products.Where(product =>
                    (product.Name != null && product.Name.ToLower().Contains(search)) ||
                    product.Variants.Any(variant => variant.Sku != null && variant.Sku.ToLower().Contains(search)));
            }

            var mappedItems = (await products
                .OrderBy(product => product.Name)
                .ToListAsync())
                .Select(product => MapProduct(product, threshold));

            if (query.OutOfStockOnly)
            {
                mappedItems = mappedItems.Where(item => item.IsOutOfStock);
            }
            else if (query.LowStockOnly)
            {
                mappedItems = mappedItems.Where(item => item.IsLowStock);
            }

            var total = mappedItems.Count();
            var items = mappedItems
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return new PagedResult<AdminInventoryItemDto>
            {
                Items = items,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = total,
            };
        }

        public async Task<ServiceResponse<AdminInventoryItemDto>> UpdateProductStockAsync(Guid productId, UpdateProductStockDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (productId == Guid.Empty)
            {
                return Failure<AdminInventoryItemDto>("Product id is required.", ServiceResponseType.ValidationError);
            }

            if (request.Quantity < 0)
            {
                return Failure<AdminInventoryItemDto>("Product quantity cannot be negative.", ServiceResponseType.ValidationError);
            }

            if (!request.ExpectedQuantity.HasValue || request.ExpectedQuantity.Value < 0)
            {
                return Failure<AdminInventoryItemDto>("The current product quantity is required.", ServiceResponseType.ValidationError);
            }

            var product = await _db.Products
                .Include(item => item.Category)
                .Include(item => item.Variants)
                .FirstOrDefaultAsync(item => item.Id == productId);

            if (product is null)
            {
                return Failure<AdminInventoryItemDto>("Product not found.", ServiceResponseType.NotFound);
            }

            if (product.Variants.Count > 0)
            {
                return Failure<AdminInventoryItemDto>(
                    "Product quantity is not authoritative when variants exist. Update variant stock instead.",
                    ServiceResponseType.ValidationError);
            }

            if (product.Quantity != request.ExpectedQuantity.Value)
            {
                return Failure<AdminInventoryItemDto>(
                    "Product stock changed after it was loaded. Refresh inventory and try again.",
                    ServiceResponseType.Conflict);
            }

            var previousQuantity = product.Quantity;
            product.Quantity = request.Quantity;
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.ChangeTracker.Clear();
                return Failure<AdminInventoryItemDto>(
                    "Product stock changed while it was being updated. Refresh inventory and try again.",
                    ServiceResponseType.Conflict);
            }

            await LogAsync("Inventory.ProductStockUpdated", "Product", product.Id.ToString(), $"Product stock updated for {product.Name}.", new { previousQuantity, product.Quantity });

            return Success(MapProduct(product, 5), "Product stock updated successfully.");
        }

        public async Task<ServiceResponse<AdminInventoryVariantDto>> UpdateVariantStockAsync(Guid variantId, UpdateVariantStockDto request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (variantId == Guid.Empty)
            {
                return Failure<AdminInventoryVariantDto>("Variant id is required.", ServiceResponseType.ValidationError);
            }

            if (request.Stock < 0)
            {
                return Failure<AdminInventoryVariantDto>("Variant stock cannot be negative.", ServiceResponseType.ValidationError);
            }

            if (!request.ExpectedStock.HasValue || request.ExpectedStock.Value < 0)
            {
                return Failure<AdminInventoryVariantDto>("The current variant stock is required.", ServiceResponseType.ValidationError);
            }

            var variant = await _db.ProductVariants
                .Include(item => item.Product)
                .FirstOrDefaultAsync(item => item.Id == variantId);

            if (variant is null)
            {
                return Failure<AdminInventoryVariantDto>("Product variant not found.", ServiceResponseType.NotFound);
            }

            if (variant.Stock != request.ExpectedStock.Value)
            {
                return Failure<AdminInventoryVariantDto>(
                    "Variant stock changed after it was loaded. Refresh inventory and try again.",
                    ServiceResponseType.Conflict);
            }

            var previousStock = variant.Stock;
            variant.Stock = request.Stock;
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.ChangeTracker.Clear();
                return Failure<AdminInventoryVariantDto>(
                    "Variant stock changed while it was being updated. Refresh inventory and try again.",
                    ServiceResponseType.Conflict);
            }

            await LogAsync("Inventory.VariantStockUpdated", "ProductVariant", variant.Id.ToString(), $"Variant stock updated for {variant.Product?.Name ?? variant.Sku ?? variant.Id.ToString()}.", new { previousStock, variant.Stock, variant.Sku });

            return Success(MapVariant(variant, 5), "Variant stock updated successfully.");
        }

        private static AdminInventoryItemDto MapProduct(Product product, int threshold)
        {
            var variants = product.Variants
                .OrderBy(variant => variant.Sku)
                .ThenBy(variant => variant.SizeValue)
                .Select(variant => MapVariant(variant, threshold))
                .ToArray();
            var productStockIsAuthoritative = variants.Length == 0;
            var isOutOfStock = productStockIsAuthoritative
                ? product.Quantity <= 0
                : variants.All(variant => variant.IsOutOfStock);
            var isLowStock = productStockIsAuthoritative
                ? product.Quantity > 0 && product.Quantity <= threshold
                : !isOutOfStock && variants.Any(variant => variant.IsLowStock);

            return new AdminInventoryItemDto
            {
                ProductId = product.Id,
                ProductName = product.Name ?? string.Empty,
                CategoryName = product.Category?.Name,
                Quantity = product.Quantity,
                ProductStockIsAuthoritative = productStockIsAuthoritative,
                VariantStock = variants.Sum(variant => variant.Stock),
                IsLowStock = isLowStock,
                IsOutOfStock = isOutOfStock,
                Variants = variants,
            };
        }

        private static AdminInventoryVariantDto MapVariant(ProductVariant variant, int threshold)
        {
            return new AdminInventoryVariantDto
            {
                VariantId = variant.Id,
                ProductId = variant.ProductId,
                ProductName = variant.Product?.Name,
                Sku = variant.Sku,
                SizeScale = variant.SizeScale.ToString(),
                SizeValue = variant.SizeValue,
                Color = variant.Color,
                Stock = variant.Stock,
                IsLowStock = variant.Stock > 0 && variant.Stock <= threshold,
                IsOutOfStock = variant.Stock <= 0,
            };
        }

        private async Task LogAsync(string action, string entityType, string entityId, string summary, object metadata)
        {
            await _auditService.LogAsync(new CreateAdminAuditLogDto
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Summary = summary,
                MetadataJson = JsonSerializer.Serialize(metadata),
            });
        }

        private static ServiceResponse<TPayload> Success<TPayload>(TPayload payload, string message)
        {
            return new ServiceResponse<TPayload>(true, message)
            {
                Payload = payload,
                ResponseType = ServiceResponseType.Success,
            };
        }

        private static ServiceResponse<TPayload> Failure<TPayload>(string message, ServiceResponseType responseType)
        {
            return new ServiceResponse<TPayload>(false, message)
            {
                ResponseType = responseType,
            };
        }
    }
}
