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
        private readonly IGenericRepository<Product> _products;
        private readonly IAppUserManager _users;

        public OrderQueryService(IOrderRepository orders, IGenericRepository<Product> products, IAppUserManager users)
        {
            _orders = orders;
            _products = products;
            _users = users;
        }

        public async Task<IEnumerable<GetOrder>> GetOrdersForUserAsync(string userId)
        {
            var list = await _orders.GetByUserIdAsync(userId);
            var map = await BuildProductNameMapAsync();
            return await MapWithUsersAsync(list, map);
        }

        public async Task<IEnumerable<GetOrder>> GetAllAsync()
        {
            var list = await _orders.GetAllAsync();
            var map = await BuildProductNameMapAsync();
            return await MapWithUsersAsync(list, map);
        }

        private async Task<Dictionary<Guid, string>> BuildProductNameMapAsync()
        {
            var all = await _products.GetAllAsync();
            return all.ToDictionary(p => p.Id, p => p.Name);
        }

        private async Task<IEnumerable<GetOrder>> MapWithUsersAsync(IEnumerable<Order> orders, IDictionary<Guid, string> nameMap)
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
                    Lines = o.Lines.Select(l => new GetOrderLine
                    {
                        ProductId = l.ProductId,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        ProductName = nameMap.TryGetValue(l.ProductId, out var n) ? n : string.Empty
                    })
                });
            }
            return result;
        }
    }
}
