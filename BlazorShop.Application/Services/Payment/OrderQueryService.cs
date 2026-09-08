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
        public OrderQueryService(IOrderRepository orders, IAppUserManager users)
        {
            _orders = orders;
            ArgumentNullException.ThrowIfNull(users);
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

        private Task<IEnumerable<GetOrder>> MapWithUsersAsync(IEnumerable<Order> orders)
        {
            var result = new List<GetOrder>();
            foreach (var o in orders)
            {
                result.Add(new GetOrder
                {
                    Id = o.Id,
                    Reference = o.Reference,
                    OrderStatus = o.OrderStatus.ToString(),
                    PaymentStatus = o.PaymentStatus.ToString(),
                    PaymentMethod = o.PaymentMethod.ToString(),
                    FulfillmentStatus = o.FulfillmentStatus.ToString(),
                    TotalAmount = o.TotalAmount,
                    SubtotalAmount = o.SubtotalAmount,
                    DiscountAmount = o.DiscountAmount,
                    ShippingAmount = o.ShippingAmount,
                    TaxAmount = o.TaxAmount,
                    Currency = o.Currency,
                    CreatedOn = o.CreatedOn,
                    ShippingCarrier = o.ShippingCarrier,
                    TrackingNumber = o.TrackingNumber,
                    TrackingUrl = o.TrackingUrl,
                    ShippedOn = o.ShippedOn,
                    DeliveredOn = o.DeliveredOn,
                    UserId = o.UserId,
                    CustomerName = o.CustomerNameSnapshot,
                    CustomerEmail = o.CustomerEmailSnapshot,
                    ShippingAddress = o.ShippingAddressSnapshot,
                    BillingAddress = o.BillingAddressSnapshot,
                    AdminNote = o.AdminNote,
                    Version = o.Version,
                    Lines = o.Lines.Select(MapLine),
                });
            }
            return Task.FromResult<IEnumerable<GetOrder>>(result);
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
