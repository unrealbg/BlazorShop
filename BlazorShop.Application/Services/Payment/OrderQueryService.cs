namespace BlazorShop.Application.Services.Payment
{
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;

    public class OrderQueryService : IOrderQueryService
    {
        private readonly IOrderRepository _orders;
        private readonly IAppUserManager _users;

        public OrderQueryService(IOrderRepository orders, IAppUserManager users)
        {
            _orders = orders;
            _users = users;
        }

        public async Task<IEnumerable<GetOrder>> GetOrdersForUserAsync(string userId)
        {
            var list = (await _orders.GetByUserIdAsync(userId)).ToList();
            return await MapWithUsersAsync(list);
        }

        public async Task<IEnumerable<GetOrder>> GetAllAsync()
        {
            var list = (await _orders.GetAllAsync()).ToList();
            return await MapWithUsersAsync(list);
        }

        private async Task<IEnumerable<GetOrder>> MapWithUsersAsync(IEnumerable<Order> orders)
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
                    Lines = o.Lines.Select(MapLine),
                });
            }
            return result;
        }

        private static GetOrderLine MapLine(OrderLine line)
        {
            return new GetOrderLine
            {
                ProductId = line.ProductId,
                VariantId = line.ProductVariantId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                LineTotal = line.LineTotal,
                ProductName = line.ProductNameSnapshot,
                Sku = line.SkuSnapshot,
                SizeScale = line.SizeScaleSnapshot,
                SizeValue = line.SizeValueSnapshot,
                Color = line.ColorSnapshot,
            };
        }
    }
}
