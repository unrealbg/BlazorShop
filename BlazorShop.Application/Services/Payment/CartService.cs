namespace BlazorShop.Application.Services.Payment
{
    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;

    public class CartService : ICartService
    {
        private readonly ICart _cart;
        private readonly IMapper _mapper;
        private readonly IProductReadRepository _productReadRepository;
        private readonly IAppUserManager _userManager;

        public CartService(
            ICart cart,
            IMapper mapper,
            IProductReadRepository productReadRepository,
            IAppUserManager userManager)
        {
            _cart = cart;
            _mapper = mapper;
            _productReadRepository = productReadRepository;
            _userManager = userManager;
        }

        public async Task<ServiceResponse> SaveCheckoutHistoryAsync(
            string userId,
            IEnumerable<CreateOrderItem> orderItems)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new ServiceResponse(false, "A signed-in user is required to save checkout history.");
            }

            var sanitizedOrderItems = orderItems
                .Where(orderItem => orderItem.ProductId != Guid.Empty && orderItem.Quantity > 0)
                .Select(orderItem => new CreateOrderItem
                {
                    ProductId = orderItem.ProductId,
                    Quantity = orderItem.Quantity,
                    UserId = userId,
                })
                .ToArray();

            if (sanitizedOrderItems.Length == 0)
            {
                return new ServiceResponse(false, "No valid checkout items were provided.");
            }

            var mappedData = _mapper.Map<IEnumerable<OrderItem>>(sanitizedOrderItems);
            var result = await _cart.SaveCheckoutHistory(mappedData);

            return result > 0
                ? new ServiceResponse(true, "Checkout history saved successfully")
                : new ServiceResponse(false, "Failed to save checkout history");
        }

        public async Task<IEnumerable<GetOrderItem>> GetOrderItemsAsync()
        {
            var history = (await _cart.GetAllCheckoutHistory())?.ToList();

            if (history is null)
            {
                return [];
            }

            var groupByCustomerId = history.GroupBy(item => item.UserId).ToList();
            var products = await _productReadRepository.GetProductsByIdsAsync(
                history.Select(item => item.ProductId));
            var orderItems = new List<GetOrderItem>();

            foreach (var customerId in groupByCustomerId)
            {
                if (string.IsNullOrWhiteSpace(customerId.Key))
                {
                    continue;
                }

                var customerDetails = await _userManager.GetUserByIdAsync(customerId.Key);

                foreach (var item in customerId)
                {
                    products.TryGetValue(item.ProductId, out var product);

                    orderItems.Add(new GetOrderItem
                    {
                        CustomerName = customerDetails?.UserName,
                        CustomerEmail = customerDetails?.Email,
                        ProductName = product?.Name,
                        AmountPayed = item.Quantity * (product?.Price ?? 0),
                        QuantityOrdered = item.Quantity,
                        DatePurchased = item.CreatedOn,
                        TrackingNumber = null,
                        TrackingUrl = null,
                        ShippingStatus = "PendingShipment",
                    });
                }
            }

            return orderItems;
        }

        public async Task<IEnumerable<GetOrderItem>> GetCheckoutHistoryByUserId(string userId)
        {
            var history = (await _cart.GetCheckoutHistoryByUserId(userId))?.ToList();

            if (history is null || history.Count == 0)
            {
                return [];
            }

            var products = await _productReadRepository.GetProductsByIdsAsync(
                history.Select(item => item.ProductId));
            var orderItems = new List<GetOrderItem>();
            var customerDetails = await _userManager.GetUserByIdAsync(userId);

            foreach (var item in history)
            {
                products.TryGetValue(item.ProductId, out var product);

                orderItems.Add(new GetOrderItem
                {
                    CustomerName = customerDetails?.UserName,
                    CustomerEmail = customerDetails?.Email,
                    ProductName = product?.Name,
                    AmountPayed = item.Quantity * (product?.Price ?? 0),
                    QuantityOrdered = item.Quantity,
                    DatePurchased = item.CreatedOn,
                    TrackingNumber = null,
                    TrackingUrl = null,
                    ShippingStatus = "PendingShipment",
                });
            }

            return orderItems;
        }
    }
}
