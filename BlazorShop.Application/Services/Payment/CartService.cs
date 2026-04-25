namespace BlazorShop.Application.Services.Payment
{
    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Payment;
    using Microsoft.Extensions.Options;

    public class CartService : ICartService
    {
        private readonly ICart _cart;
        private readonly IMapper _mapper;
        private readonly IProductReadRepository _productReadRepository;
        private readonly IPaymentMethodService _paymentMethodService;
        private readonly IPaymentService _paymentService; // Stripe/Card
        private readonly IPayPalPaymentService _payPalPaymentService; // PayPal
        private readonly IAppUserManager _userManager;
        private readonly IOrderRepository _orderRepository;
        private readonly IEmailService _emailService;
        private readonly BankTransferSettings _btSettings;

        public CartService(ICart cart,
                           IMapper mapper,
                           IProductReadRepository productReadRepository,
                           IPaymentMethodService paymentMethodService,
                           IPaymentService paymentService,
                           IPayPalPaymentService payPalPaymentService,
                           IAppUserManager userManager,
                           IOrderRepository orderRepository,
                           IEmailService emailService,
                           IOptions<BankTransferSettings> bankTransferOptions)
        {
            _cart = cart;
            _mapper = mapper;
            _productReadRepository = productReadRepository;
            _paymentMethodService = paymentMethodService;
            _paymentService = paymentService;
            _payPalPaymentService = payPalPaymentService;
            _userManager = userManager;
            _orderRepository = orderRepository;
            _emailService = emailService;
            _btSettings = bankTransferOptions.Value;
        }

        public async Task<ServiceResponse> SaveCheckoutHistoryAsync(string userId, IEnumerable<CreateOrderItem> orderItems)
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

            return result > 0 ? new ServiceResponse(true, "Checkout history saved successfully") : new ServiceResponse(false, "Failed to save checkout history");
        }

        public async Task<ServiceResponse> ConfirmOrderAsync(IEnumerable<ProcessCart> carts, string userId, string? status = null)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new ServiceResponse(false, "A signed-in user is required to confirm the order.");
            }

            return await CreateOrderAsync(carts, userId, status ?? "Paid", "ORD");
        }

        public async Task<ServiceResponse> CheckoutAsync(Checkout checkout)
        {
            return await CheckoutAsync(checkout, null);
        }

        public async Task<ServiceResponse> CheckoutAsync(Checkout checkout, string? userId)
        {
            var (products, totalAmount) = await this.GetCartTotalAmount(checkout.Carts);
            var methods = (await _paymentMethodService.GetPaymentMethodsAsync()).ToList();
            if (!methods.Any()) return new ServiceResponse(false, "No payment methods available");

            var creditCardId = methods.FirstOrDefault(m => m.Name == "Credit Card")?.Id;
            var payPalId = methods.FirstOrDefault(m => m.Name == "PayPal")?.Id;
            var codId = methods.FirstOrDefault(m => m.Name == "Cash on Delivery")?.Id;
            var bankId = methods.FirstOrDefault(m => m.Name == "Bank Transfer")?.Id;

            if (creditCardId.HasValue && checkout.PaymentMethodId == creditCardId.Value)
            {
                return await _paymentService.Pay(totalAmount, products, checkout.Carts);
            }
            if (payPalId.HasValue && checkout.PaymentMethodId == payPalId.Value)
            {
                return await _payPalPaymentService.Pay(totalAmount, products, checkout.Carts);
            }
            if (codId.HasValue && checkout.PaymentMethodId == codId.Value)
            {
                return new ServiceResponse(true, "Order placed with Cash on Delivery. You will pay upon delivery.");
            }
            if (bankId.HasValue && checkout.PaymentMethodId == bankId.Value)
            {
                var reference = $"BT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
                var orderResult = await CreateOrderAsync(checkout.Carts, userId, "Pending", "BT", reference);

                if (!orderResult.Success)
                {
                    return orderResult;
                }

                try
                {
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var user = await _userManager.GetUserByIdAsync(userId);
                        if (user != null && !string.IsNullOrEmpty(user.Email))
                        {
                            var iban = string.IsNullOrWhiteSpace(_btSettings.Iban) ? "BG00UNCR70001512345678" : _btSettings.Iban;
                            var html = $@"<p>Thank you for your order.</p>
<p>Please make a bank transfer to the following account:</p>
<ul>
<li>Bank: <b>{_btSettings.BankName}</b></li>
<li>Beneficiary: <b>{_btSettings.Beneficiary}</b></li>
<li>IBAN: <b>{iban}</b></li>
<li>Amount: <b>{totalAmount:F2} EUR</b></li>
<li>Reference: <b>{reference}</b></li>
</ul>
<p>{_btSettings.AdditionalInfo}</p>
<p>Your order will be processed once we receive the payment.</p>";
                            await _emailService.SendEmailAsync(user.Email, "Bank Transfer Instructions", html);
                        }
                    }
                }
                catch
                {
                    // ignored
                }

                var info = new BankTransferInfo
                {
                    Iban = _btSettings.Iban,
                    Beneficiary = _btSettings.Beneficiary,
                    BankName = _btSettings.BankName,
                    Reference = reference,
                    Amount = totalAmount,
                    AdditionalInfo = _btSettings.AdditionalInfo
                };

                return new ServiceResponse(true, "Bank Transfer selected. Please check your email for payment instructions.")
                {
                    Payload = info
                };
            }

            return new ServiceResponse(false, "Invalid payment method");
        }

        private async Task<ServiceResponse> CreateOrderAsync(
            IEnumerable<ProcessCart> carts,
            string? userId,
            string status,
            string referencePrefix,
            string? reference = null)
        {
            var cartList = carts
                .Where(item => item.ProductId != Guid.Empty && item.Quantity > 0)
                .ToList();

            if (cartList.Count == 0)
            {
                return new ServiceResponse(false, "Your cart is empty.");
            }

            var (products, totalAmount) = await GetCartTotalAmount(cartList);
            var productMap = products.ToDictionary(product => product.Id);

            if (productMap.Count == 0 || totalAmount <= 0)
            {
                return new ServiceResponse(false, "We couldn't resolve the cart items for this order.");
            }

            var order = new Order
            {
                UserId = userId ?? string.Empty,
                Status = status,
                Reference = reference ?? $"{referencePrefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                TotalAmount = totalAmount,
                Lines = cartList
                    .Where(item => productMap.ContainsKey(item.ProductId))
                    .Select(item => new OrderLine
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = productMap[item.ProductId].Price,
                    })
                    .ToList(),
            };

            if (order.Lines.Count == 0)
            {
                return new ServiceResponse(false, "We couldn't resolve the cart items for this order.");
            }

            await _orderRepository.CreateAsync(order);

            return new ServiceResponse(true, "Order saved successfully")
            {
                Payload = new
                {
                    order.Id,
                    order.Reference,
                }
            };
        }

        public async Task<IEnumerable<GetOrderItem>> GetOrderItemsAsync()
        {
            var history = (await _cart.GetAllCheckoutHistory())?.ToList();

            if (history == null)
            {
                return [];
            }

            var groupByCustomerId = history.GroupBy(x => x.UserId).ToList();
            var products = await _productReadRepository.GetProductsByIdsAsync(history.Select(item => item.ProductId));
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
                        ShippingStatus = "PendingShipment"
                    });
                }
            }

            return orderItems;
        }

        private async Task<(IEnumerable<Product>, decimal)> GetCartTotalAmount(IEnumerable<ProcessCart> carts)
        {
            var cartList = carts.ToList();

            if (cartList.Count == 0)
            {
                return ([], 0);
            }

            var productLookup = await _productReadRepository.GetProductsByIdsAsync(cartList.Select(item => item.ProductId));

            if (productLookup.Count == 0)
            {
                return ([], 0);
            }

            var cartProducts = productLookup.Values.ToList();

            var totalAmount = cartList.Sum(item =>
                productLookup.TryGetValue(item.ProductId, out var product)
                    ? item.Quantity * product.Price
                    : 0);

            return (cartProducts, totalAmount);
        }

        public async Task<IEnumerable<GetOrderItem>> GetCheckoutHistoryByUserId(string userId)
        {
            var history = (await _cart.GetCheckoutHistoryByUserId(userId))?.ToList();

            if (history == null || !history.Any())
            {
                return new List<GetOrderItem>();
            }

            var products = await _productReadRepository.GetProductsByIdsAsync(history.Select(item => item.ProductId));

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
                                       ShippingStatus = "PendingShipment"
                                   });
            }

            return orderItems;
        }
    }
}
