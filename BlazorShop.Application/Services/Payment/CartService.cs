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

        public async Task<ServiceResponse> ConfirmOrderAsync(IEnumerable<CartLineRequest> carts, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new ServiceResponse(false, "A signed-in user is required to confirm the order.");
            }

            var resolution = await ResolveCartLinesAsync(carts);
            return resolution.Error ?? await CreateOrderAsync(resolution.Lines, userId, "Pending", "COD");
        }

        public async Task<ServiceResponse> CheckoutAsync(Checkout checkout)
        {
            return await CheckoutAsync(checkout, null);
        }

        public async Task<ServiceResponse> CheckoutAsync(Checkout checkout, string? userId)
        {
            var methods = (await _paymentMethodService.GetPaymentMethodsAsync()).ToList();
            if (!methods.Any()) return new ServiceResponse(false, "No payment methods available");

            var creditCardId = methods.FirstOrDefault(m => m.Name == "Credit Card")?.Id;
            var payPalId = methods.FirstOrDefault(m => m.Name == "PayPal")?.Id;
            var codId = methods.FirstOrDefault(m => m.Name == "Cash on Delivery")?.Id;
            var bankId = methods.FirstOrDefault(m => m.Name == "Bank Transfer")?.Id;

            var isCreditCard = creditCardId.HasValue && checkout.PaymentMethodId == creditCardId.Value;
            var isPayPal = payPalId.HasValue && checkout.PaymentMethodId == payPalId.Value;
            var isCashOnDelivery = codId.HasValue && checkout.PaymentMethodId == codId.Value;
            var isBankTransfer = bankId.HasValue && checkout.PaymentMethodId == bankId.Value;

            if (!isCreditCard && !isPayPal && !isCashOnDelivery && !isBankTransfer)
            {
                return new ServiceResponse(false, "Invalid payment method");
            }

            var resolution = await ResolveCartLinesAsync(checkout.Carts);
            if (resolution.Error is not null)
            {
                return resolution.Error;
            }

            var resolvedLines = resolution.Lines;
            var totalAmount = resolvedLines.Sum(line => line.LineTotal);

            if (isCreditCard)
            {
                var pendingOrder = await CreateOrderAsync(
                    resolvedLines,
                    userId,
                    PaymentOrderStatus.PendingPayment,
                    "STRIPE");

                if (!pendingOrder.Success || !pendingOrder.Id.HasValue)
                {
                    return pendingOrder;
                }

                var paymentResult = await _paymentService.Pay(resolvedLines, pendingOrder.Id.Value);

                if (!paymentResult.Success)
                {
                    await _orderRepository.UpdatePaymentStatusAsync(
                        pendingOrder.Id.Value,
                        PaymentOrderStatus.PaymentFailed);
                }

                return paymentResult;
            }
            if (isPayPal)
            {
                return await _payPalPaymentService.Pay(resolvedLines);
            }
            if (isCashOnDelivery)
            {
                return new ServiceResponse(true, "Order placed with Cash on Delivery. You will pay upon delivery.");
            }
            if (isBankTransfer)
            {
                var reference = $"BT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}";
                var orderResult = await CreateOrderAsync(resolvedLines, userId, "Pending", "BT", reference);

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
            IReadOnlyCollection<ResolvedCartLine> lines,
            string? userId,
            string status,
            string referencePrefix,
            string? reference = null)
        {
            if (lines.Count == 0)
            {
                return new ServiceResponse(false, "Your cart is empty.");
            }

            var order = new Order
            {
                UserId = userId ?? string.Empty,
                Status = status,
                Reference = reference ?? $"{referencePrefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                TotalAmount = lines.Sum(line => line.LineTotal),
                Lines = lines
                    .Select(line => new OrderLine
                    {
                        ProductId = line.ProductId,
                        ProductVariantId = line.VariantId,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                    })
                    .ToList(),
            };

            await _orderRepository.CreateAsync(order);

            return new ServiceResponse(true, "Order saved successfully", order.Id)
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

        private async Task<CartLineResolution> ResolveCartLinesAsync(IEnumerable<CartLineRequest> carts)
        {
            var cartList = carts?.ToList() ?? [];

            if (cartList.Count == 0)
            {
                return CartLineResolution.Failure("Your cart is empty.");
            }

            if (cartList.Any(line => line.ProductId == Guid.Empty || line.Quantity <= 0))
            {
                return CartLineResolution.Failure("Every cart item must have a valid product and quantity.");
            }

            var productLookup = await _productReadRepository.GetProductsByIdsAsync(cartList.Select(line => line.ProductId));
            var variantLookup = await _productReadRepository.GetProductVariantsByIdsAsync(
                cartList.Where(line => line.VariantId.HasValue).Select(line => line.VariantId!.Value));
            var resolvedLines = new List<ResolvedCartLine>(cartList.Count);

            foreach (var line in cartList)
            {
                if (!productLookup.TryGetValue(line.ProductId, out var product))
                {
                    return CartLineResolution.Failure("A product in the cart no longer exists.");
                }

                if (!product.IsPublished || product.PublishedOn is null)
                {
                    return CartLineResolution.Failure("A product in the cart is not currently purchasable.");
                }

                ProductVariant? variant = null;
                if (line.VariantId.HasValue)
                {
                    if (!variantLookup.TryGetValue(line.VariantId.Value, out variant))
                    {
                        return CartLineResolution.Failure("A selected product variant no longer exists.");
                    }

                    if (variant.ProductId != line.ProductId)
                    {
                        return CartLineResolution.Failure("A selected product variant does not belong to the requested product.");
                    }

                    if (variant.Stock <= 0)
                    {
                        return CartLineResolution.Failure("A selected product variant is not currently purchasable.");
                    }
                }
                else if (product.Quantity <= 0)
                {
                    return CartLineResolution.Failure("A product in the cart is not currently purchasable.");
                }

                var unitPrice = variant?.Price ?? product.Price;
                if (unitPrice <= 0)
                {
                    return CartLineResolution.Failure("A product in the cart does not have a valid current price.");
                }

                resolvedLines.Add(new ResolvedCartLine(
                    product.Id,
                    variant?.Id,
                    line.Quantity,
                    unitPrice,
                    product.Name ?? "Product",
                    product.Description,
                    variant?.Sku,
                    variant?.SizeValue,
                    variant?.Color));
            }

            return CartLineResolution.Success(resolvedLines);
        }

        private sealed record CartLineResolution(IReadOnlyList<ResolvedCartLine> Lines, ServiceResponse? Error)
        {
            public static CartLineResolution Success(IReadOnlyList<ResolvedCartLine> lines) => new(lines, null);

            public static CartLineResolution Failure(string message) => new([], new ServiceResponse(false, message));
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
