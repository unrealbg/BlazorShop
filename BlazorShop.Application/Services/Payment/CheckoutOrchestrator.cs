namespace BlazorShop.Application.Services.Payment
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Authentication;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Identity;
    using BlazorShop.Domain.Entities.Payment;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public sealed class CheckoutOrchestrator : ICheckoutOrchestrator
    {
        private readonly IProductReadRepository _productReadRepository;
        private readonly IPaymentMethodService _paymentMethodService;
        private readonly IPaymentService _paymentService;
        private readonly IAppUserManager _userManager;
        private readonly IInventoryReservationService _inventoryReservationService;
        private readonly IEmailService _emailService;
        private readonly BankTransferSettings _bankTransferSettings;
        private readonly ILogger<CheckoutOrchestrator> _logger;

        public CheckoutOrchestrator(
            IProductReadRepository productReadRepository,
            IPaymentMethodService paymentMethodService,
            IPaymentService paymentService,
            IAppUserManager userManager,
            IInventoryReservationService inventoryReservationService,
            IEmailService emailService,
            IOptions<BankTransferSettings> bankTransferOptions,
            ILogger<CheckoutOrchestrator> logger)
        {
            _productReadRepository = productReadRepository;
            _paymentMethodService = paymentMethodService;
            _paymentService = paymentService;
            _userManager = userManager;
            _inventoryReservationService = inventoryReservationService;
            _emailService = emailService;
            _bankTransferSettings = bankTransferOptions.Value;
            _logger = logger;
        }

        public async Task<ServiceResponse<CheckoutResult>> CheckoutAsync(
            Checkout checkout,
            string userId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Failure("A signed-in user is required to place an order.");
            }

            if (checkout is null)
            {
                return Failure("Checkout details are required.");
            }

            var customer = await _userManager.GetUserByIdAsync(userId);
            if (customer is null)
            {
                return Failure("The authenticated customer account could not be found.");
            }

            var paymentKind = await ResolvePaymentKindAsync(checkout.PaymentMethodId);
            if (!paymentKind.HasValue)
            {
                return Failure("Invalid payment method");
            }

            var resolution = await ResolveCartLinesAsync(checkout.Carts);
            if (resolution.ErrorMessage is not null)
            {
                return Failure(resolution.ErrorMessage);
            }

            return paymentKind.Value switch
            {
                CheckoutPaymentKind.CashOnDelivery => await CheckoutCashOnDeliveryAsync(
                    resolution.Lines,
                    userId,
                    cancellationToken),
                CheckoutPaymentKind.BankTransfer => await CheckoutBankTransferAsync(
                    resolution.Lines,
                    userId,
                    customer,
                    cancellationToken),
                CheckoutPaymentKind.Stripe => await CheckoutStripeAsync(
                    resolution.Lines,
                    userId,
                    cancellationToken),
                _ => Failure("Invalid payment method"),
            };
        }

        private async Task<CheckoutPaymentKind?> ResolvePaymentKindAsync(Guid paymentMethodId)
        {
            var availableMethods = await _paymentMethodService.GetPaymentMethodsAsync();
            if (!availableMethods.Any(method => method.Id == paymentMethodId))
            {
                return null;
            }

            if (paymentMethodId == PaymentMethodIds.CreditCard)
            {
                return CheckoutPaymentKind.Stripe;
            }

            if (paymentMethodId == PaymentMethodIds.CashOnDelivery)
            {
                return CheckoutPaymentKind.CashOnDelivery;
            }

            if (paymentMethodId == PaymentMethodIds.BankTransfer)
            {
                return CheckoutPaymentKind.BankTransfer;
            }

            return null;
        }

        private async Task<ServiceResponse<CheckoutResult>> CheckoutCashOnDeliveryAsync(
            IReadOnlyCollection<ResolvedCartLine> lines,
            string userId,
            CancellationToken cancellationToken)
        {
            var creation = await CreateOrderAsync(
                lines,
                userId,
                "Pending",
                "COD",
                InventoryReservationStatus.Consumed,
                cancellationToken);

            return creation.Order is null
                ? Failure(creation.ErrorMessage!)
                : Success(
                    creation.Order,
                    CheckoutStatus.Confirmed,
                    CheckoutPaymentKind.CashOnDelivery,
                    "Order placed with Cash on Delivery. You will pay upon delivery.");
        }

        private async Task<ServiceResponse<CheckoutResult>> CheckoutBankTransferAsync(
            IReadOnlyCollection<ResolvedCartLine> lines,
            string userId,
            AppUser customer,
            CancellationToken cancellationToken)
        {
            var reference = CreateReference("BT");
            var creation = await CreateOrderAsync(
                lines,
                userId,
                "Pending",
                "BT",
                InventoryReservationStatus.Reserved,
                cancellationToken,
                reference);

            if (creation.Order is null)
            {
                return Failure(creation.ErrorMessage!);
            }

            var transferInfo = BuildBankTransferInfo(creation.Order);
            await TrySendBankTransferEmailAsync(customer, transferInfo);

            return Success(
                creation.Order,
                CheckoutStatus.PendingPayment,
                CheckoutPaymentKind.BankTransfer,
                "Order placed. Complete the bank transfer using the supplied instructions.",
                bankTransfer: transferInfo);
        }

        private async Task<ServiceResponse<CheckoutResult>> CheckoutStripeAsync(
            IReadOnlyCollection<ResolvedCartLine> lines,
            string userId,
            CancellationToken cancellationToken)
        {
            var creation = await CreateOrderAsync(
                lines,
                userId,
                PaymentOrderStatus.PendingPayment,
                "STRIPE",
                InventoryReservationStatus.Reserved,
                cancellationToken);

            if (creation.Order is null)
            {
                return Failure(creation.ErrorMessage!);
            }

            PaymentInitializationResult paymentResult;
            try
            {
                paymentResult = await _paymentService.Pay(
                    lines,
                    creation.Order.Id,
                    creation.Order.Reference);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Order {OrderId} was created, but Stripe initialization threw an exception.",
                    creation.Order.Id);
                paymentResult = new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Unable to initialize the card payment session. Please try again later.");
            }

            if (!paymentResult.Success || string.IsNullOrWhiteSpace(paymentResult.RedirectUrl))
            {
                var releaseResult = await _inventoryReservationService.TransitionOrderAsync(
                    creation.Order.Id,
                    PaymentOrderStatus.PaymentFailed,
                    InventoryReservationStatus.Released,
                    cancellationToken);

                if (!releaseResult.Success)
                {
                    return Failure(
                        releaseResult.ErrorMessage
                        ?? "Unable to safely release inventory after payment initialization failed.");
                }

                return Failure(
                    paymentResult.ErrorMessage
                    ?? "Unable to initialize the card payment session. Please try again later.");
            }

            return Success(
                creation.Order,
                CheckoutStatus.PendingPayment,
                CheckoutPaymentKind.Stripe,
                "Order created. Continue to the secure card payment page.",
                paymentResult.RedirectUrl);
        }

        private async Task<OrderCreationResult> CreateOrderAsync(
            IReadOnlyCollection<ResolvedCartLine> lines,
            string userId,
            string status,
            string referencePrefix,
            InventoryReservationStatus reservationStatus,
            CancellationToken cancellationToken,
            string? reference = null)
        {
            if (lines.Count == 0)
            {
                return OrderCreationResult.Failure("Your cart is empty.");
            }

            var order = new Order
            {
                UserId = userId,
                Status = status,
                Reference = reference ?? CreateReference(referencePrefix),
                TotalAmount = lines.Sum(line => line.LineTotal),
                Lines = lines
                    .Select(line => new OrderLine
                    {
                        ProductId = line.ProductId,
                        ProductVariantId = line.VariantId,
                        ProductNameSnapshot = line.ProductName,
                        SkuSnapshot = line.Sku,
                        SizeScaleSnapshot = line.SizeScale?.ToString(),
                        SizeValueSnapshot = line.SizeValue,
                        ColorSnapshot = line.Color,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        LineTotal = line.LineTotal,
                    })
                    .ToList(),
            };

            var inventoryResult = await _inventoryReservationService.CreateOrderWithInventoryAsync(
                order,
                reservationStatus,
                cancellationToken);
            return inventoryResult.Success
                ? OrderCreationResult.Success(order)
                : OrderCreationResult.Failure(
                    inventoryResult.ErrorMessage ?? "Unable to reserve the requested inventory.");
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

            var productLookup = await _productReadRepository.GetProductsByIdsAsync(
                cartList.Select(line => line.ProductId));
            var variantLookup = await _productReadRepository.GetProductVariantsByIdsAsync(
                cartList.Where(line => line.VariantId.HasValue).Select(line => line.VariantId!.Value));
            var productIdsWithVariants = await _productReadRepository.GetProductIdsWithVariantsAsync(
                cartList.Select(line => line.ProductId));
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
                        return CartLineResolution.Failure(
                            "A selected product variant does not belong to the requested product.");
                    }

                    if (variant.Stock <= 0)
                    {
                        return CartLineResolution.Failure(
                            "A selected product variant is not currently purchasable.");
                    }

                    if (line.Quantity > variant.Stock)
                    {
                        return CartLineResolution.Failure(
                            "The requested quantity exceeds the selected product variant's current availability.");
                    }
                }
                else
                {
                    if (productIdsWithVariants.Contains(line.ProductId))
                    {
                        return CartLineResolution.Failure("A product variant must be selected for this product.");
                    }

                    if (product.Quantity <= 0)
                    {
                        return CartLineResolution.Failure("A product in the cart is not currently purchasable.");
                    }

                    if (line.Quantity > product.Quantity)
                    {
                        return CartLineResolution.Failure(
                            "The requested quantity exceeds the product's current availability.");
                    }
                }

                var unitPrice = variant?.Price ?? product.Price;
                if (unitPrice <= 0)
                {
                    return CartLineResolution.Failure(
                        "A product in the cart does not have a valid current price.");
                }

                resolvedLines.Add(new ResolvedCartLine(
                    product.Id,
                    variant?.Id,
                    line.Quantity,
                    unitPrice,
                    product.Name ?? "Product",
                    product.Description,
                    variant?.Sku,
                    variant?.SizeScale,
                    variant?.SizeValue,
                    variant?.Color));
            }

            return CartLineResolution.Success(resolvedLines);
        }

        private BankTransferInfo BuildBankTransferInfo(Order order)
        {
            return new BankTransferInfo
            {
                Iban = string.IsNullOrWhiteSpace(_bankTransferSettings.Iban)
                    ? "BG00UNCR70001512345678"
                    : _bankTransferSettings.Iban,
                Beneficiary = _bankTransferSettings.Beneficiary,
                BankName = _bankTransferSettings.BankName,
                Reference = order.Reference,
                Amount = order.TotalAmount,
                AdditionalInfo = _bankTransferSettings.AdditionalInfo,
            };
        }

        private async Task TrySendBankTransferEmailAsync(AppUser customer, BankTransferInfo info)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(customer.Email))
                {
                    return;
                }

                var html = $@"<p>Thank you for your order.</p>
<p>Please make a bank transfer to the following account:</p>
<ul>
<li>Bank: <b>{info.BankName}</b></li>
<li>Beneficiary: <b>{info.Beneficiary}</b></li>
<li>IBAN: <b>{info.Iban}</b></li>
<li>Amount: <b>{info.Amount:F2} EUR</b></li>
<li>Reference: <b>{info.Reference}</b></li>
</ul>
<p>{info.AdditionalInfo}</p>
<p>Your order will be processed once we receive the payment.</p>";
                await _emailService.SendEmailAsync(
                    customer.Email,
                    "Bank Transfer Instructions",
                    html);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Order {OrderReference} was created, but its bank-transfer email could not be sent.",
                    info.Reference);
            }
        }

        private static string CreateReference(string prefix) =>
            $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";

        private static ServiceResponse<CheckoutResult> Success(
            Order order,
            CheckoutStatus status,
            CheckoutPaymentKind paymentKind,
            string message,
            string? redirectUrl = null,
            BankTransferInfo? bankTransfer = null)
        {
            return new ServiceResponse<CheckoutResult>(true, message, order.Id)
            {
                Payload = new CheckoutResult(
                    order.Id,
                    order.Reference,
                    status,
                    paymentKind,
                    redirectUrl,
                    bankTransfer),
            };
        }

        private static ServiceResponse<CheckoutResult> Failure(string message) => new(false, message);

        private sealed record CartLineResolution(
            IReadOnlyList<ResolvedCartLine> Lines,
            string? ErrorMessage)
        {
            public static CartLineResolution Success(IReadOnlyList<ResolvedCartLine> lines) => new(lines, null);

            public static CartLineResolution Failure(string message) => new([], message);
        }

        private sealed record OrderCreationResult(Order? Order, string? ErrorMessage)
        {
            public static OrderCreationResult Success(Order order) => new(order, null);

            public static OrderCreationResult Failure(string message) => new(null, message);
        }
    }
}
