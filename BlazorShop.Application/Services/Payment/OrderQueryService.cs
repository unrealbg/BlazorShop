namespace BlazorShop.Application.Services.Payment
{
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Payment;

    public class OrderQueryService : IOrderQueryService
    {
        private readonly IOrderRepository _orders;
        private readonly IProductReadRepository _products;
        private readonly IAppUserManager _users;

        public OrderQueryService(IOrderRepository orders, IProductReadRepository products, IAppUserManager users)
        {
            _orders = orders;
            _products = products;
            _users = users;
        }

        public async Task<IEnumerable<GetOrder>> GetOrdersForUserAsync(string userId)
        {
            var list = (await _orders.GetByUserIdAsync(userId)).ToList();
            var catalog = await BuildCatalogMapsAsync(list);
            return await MapWithUsersAsync(list, catalog);
        }

        public async Task<IEnumerable<GetOrder>> GetAllAsync()
        {
            var list = (await _orders.GetAllAsync()).ToList();
            var catalog = await BuildCatalogMapsAsync(list);
            return await MapWithUsersAsync(list, catalog);
        }

        private async Task<OrderCatalogMaps> BuildCatalogMapsAsync(IEnumerable<Order> orders)
        {
            var products = await _products.GetProductsByIdsAsync(
                orders.SelectMany(order => order.Lines).Select(line => line.ProductId));
            var variants = await _products.GetProductVariantsByIdsAsync(
                orders.SelectMany(order => order.Lines)
                    .Where(line => line.ProductVariantId.HasValue)
                    .Select(line => line.ProductVariantId!.Value));

            return new OrderCatalogMaps(
                products.ToDictionary(entry => entry.Key, entry => entry.Value.Name ?? string.Empty),
                variants);
        }

        private async Task<IEnumerable<GetOrder>> MapWithUsersAsync(IEnumerable<Order> orders, OrderCatalogMaps catalog)
        {
            var result = new List<GetOrder>();
            foreach (var o in orders)
            {
                string? userName = null;
                string? email = null;
                if (!string.IsNullOrWhiteSpace(o.UserId))
                {
                    try
                    {
                        var u = await _users.GetUserByIdAsync(o.UserId);
                        userName = u?.UserName;
                        email = u?.Email;
                    }
                    catch { }
                }

                result.Add(new GetOrder
                {
                    Id = o.Id,
                    Reference = o.Reference,
                    Status = o.Status,
                    TotalAmount = o.TotalAmount,
                    CreatedOn = o.CreatedOn,
                    ShippingStatus = o.ShippingStatus,
                    ShippingCarrier = o.ShippingCarrier,
                    TrackingNumber = o.TrackingNumber,
                    TrackingUrl = o.TrackingUrl,
                    ShippedOn = o.ShippedOn,
                    DeliveredOn = o.DeliveredOn,
                    UserId = o.UserId,
                    CustomerName = userName,
                    CustomerEmail = email,
                    AdminNote = o.AdminNote,
                    Lines = o.Lines.Select(line => MapLine(line, catalog)),
                });
            }
            return result;
        }

        private static GetOrderLine MapLine(OrderLine line, OrderCatalogMaps catalog)
        {
            ProductVariant? variant = null;
            if (line.ProductVariantId.HasValue)
            {
                catalog.Variants.TryGetValue(line.ProductVariantId.Value, out variant);
            }

            return new GetOrderLine
            {
                ProductId = line.ProductId,
                VariantId = line.ProductVariantId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                ProductName = catalog.ProductNames.TryGetValue(line.ProductId, out var name) ? name : string.Empty,
                Sku = variant?.Sku,
                SizeValue = variant?.SizeValue,
                Color = variant?.Color,
            };
        }

        private sealed record OrderCatalogMaps(
            IReadOnlyDictionary<Guid, string> ProductNames,
            IReadOnlyDictionary<Guid, ProductVariant> Variants);
    }
}
