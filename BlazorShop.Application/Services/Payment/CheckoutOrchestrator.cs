namespace BlazorShop.Application.Services.Payment
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Options;
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
        private const int OutcomeVersion = 1;
        private const int StripeInitializationVersion = 2;
        private readonly IProductReadRepository _productReadRepository;
        private readonly IPaymentMethodService _paymentMethodService;
        private readonly IPaymentService _paymentService;
        private readonly IAppUserManager _userManager;
        private readonly IInventoryReservationService _inventoryReservationService;
        private readonly ICheckoutIdempotencyStore _idempotencyStore;
        private readonly IPaymentTransactionStore _paymentTransactionStore;
        private readonly IStripePaymentStateTransitionService _stripePaymentStateTransitionService;
        private readonly IOrderRepository _orderRepository;
        private readonly IEmailService _emailService;
        private readonly BankTransferSettings _bankTransferSettings;
        private readonly ClientAppOptions _clientAppOptions;
        private readonly CheckoutIdempotencyOptions _idempotencyOptions;
        private readonly CommerceOptions _commerceOptions;
        private readonly ILogger<CheckoutOrchestrator> _logger;

        public CheckoutOrchestrator(
            IProductReadRepository productReadRepository,
            IPaymentMethodService paymentMethodService,
            IPaymentService paymentService,
            IAppUserManager userManager,
            IInventoryReservationService inventoryReservationService,
            ICheckoutIdempotencyStore idempotencyStore,
            IPaymentTransactionStore paymentTransactionStore,
            IStripePaymentStateTransitionService stripePaymentStateTransitionService,
            IOrderRepository orderRepository,
            IEmailService emailService,
            IOptions<BankTransferSettings> bankTransferOptions,
            IOptions<ClientAppOptions> clientAppOptions,
            IOptions<CheckoutIdempotencyOptions> idempotencyOptions,
            IOptions<CommerceOptions> commerceOptions,
            ILogger<CheckoutOrchestrator> logger)
        {
            _productReadRepository = productReadRepository;
            _paymentMethodService = paymentMethodService;
            _paymentService = paymentService;
            _userManager = userManager;
            _inventoryReservationService = inventoryReservationService;
            _idempotencyStore = idempotencyStore;
            _paymentTransactionStore = paymentTransactionStore;
            _stripePaymentStateTransitionService = stripePaymentStateTransitionService;
            _orderRepository = orderRepository;
            _emailService = emailService;
            _bankTransferSettings = bankTransferOptions.Value;
            _clientAppOptions = clientAppOptions.Value;
            _idempotencyOptions = idempotencyOptions.Value;
            _commerceOptions = commerceOptions.Value;
            _logger = logger;
        }

        public async Task<CheckoutExecutionResult> CheckoutAsync(
            Checkout checkout,
            string userId,
            Guid idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest("A signed-in user is required to place an order.");
            }

            if (checkout is null)
            {
                return BadRequest("Checkout details are required.");
            }

            var canonicalization = CheckoutIntentCanonicalizer.Canonicalize(checkout);
            if (!canonicalization.IsValid)
            {
                return BadRequest(canonicalization.ErrorMessage!);
            }

            var customer = await _userManager.GetUserByIdAsync(userId);
            if (customer is null)
            {
                return BadRequest("The authenticated customer account could not be found.");
            }

            var claim = await _idempotencyStore.ClaimAsync(
                userId,
                idempotencyKey,
                canonicalization.Fingerprint!,
                checkout.PaymentMethodId,
                cancellationToken);

            if (claim.Status == CheckoutIdempotencyClaimStatus.Conflict)
            {
                return CheckoutExecutionResult.Conflict(
                    "The Idempotency-Key was already used for different checkout intent.");
            }

            if (claim.Status == CheckoutIdempotencyClaimStatus.InProgress)
            {
                return CheckoutExecutionResult.InProgress();
            }

            if (claim.Status == CheckoutIdempotencyClaimStatus.Replayed)
            {
                return await ReplayPersistedOutcomeAsync(claim.Outcome!, cancellationToken);
            }

            var normalizedCheckout = new Checkout
            {
                PaymentMethodId = checkout.PaymentMethodId,
                Carts = canonicalization.Lines,
            };
            return await ExecuteOwnedCheckoutAsync(
                normalizedCheckout,
                userId,
                customer,
                claim,
                cancellationToken);
        }

        private async Task<CheckoutExecutionResult> ExecuteOwnedCheckoutAsync(
            Checkout checkout,
            string userId,
            AppUser customer,
            CheckoutIdempotencyClaim claim,
            CancellationToken cancellationToken)
        {
            var paymentKind = await ResolvePaymentKindAsync(checkout.PaymentMethodId);
            if (!paymentKind.HasValue)
            {
                return await FinishFailureAsync(claim, "Invalid payment method", cancellationToken);
            }

            var existingOrder = await _orderRepository.GetByIdAsync(claim.Record.OrderId);
            if (existingOrder is not null)
            {
                if (!string.Equals(existingOrder.UserId, userId, StringComparison.Ordinal)
                    || !string.Equals(existingOrder.Reference, claim.Record.OrderReference, StringComparison.Ordinal))
                {
                    return await FinishFailureAsync(
                        claim,
                        "The existing order does not match the durable checkout identity.",
                        cancellationToken);
                }

                return await ResumeCommittedOrderAsync(
                    existingOrder,
                    paymentKind.Value,
                    customer,
                    claim,
                    cancellationToken);
            }

            var resolution = await ResolveCartLinesAsync(checkout.Carts);
            if (resolution.ErrorMessage is not null)
            {
                return await FinishFailureAsync(claim, resolution.ErrorMessage, cancellationToken);
            }

            Order order;
            try
            {
                order = CreateOrder(
                    resolution.Lines,
                    userId,
                    claim.Record.OrderId,
                    claim.Record.OrderReference,
                    paymentKind.Value,
                    _commerceOptions.Currency);
            }
            catch (Exception exception) when (exception is OverflowException or ArgumentOutOfRangeException)
            {
                return await FinishFailureAsync(
                    claim,
                    "The order amount cannot be represented safely in the configured currency.",
                    cancellationToken);
            }
            var successOutcome = paymentKind.Value switch
            {
                CheckoutPaymentKind.CashOnDelivery => CreateSuccessOutcome(
                    order,
                    CheckoutStatus.Confirmed,
                    CheckoutPaymentKind.CashOnDelivery,
                    "Order placed with Cash on Delivery. You will pay upon delivery."),
                CheckoutPaymentKind.BankTransfer => CreateSuccessOutcome(
                    order,
                    CheckoutStatus.PendingPayment,
                    CheckoutPaymentKind.BankTransfer,
                    "Order placed. Complete the bank transfer using the supplied instructions.",
                    bankTransfer: BuildBankTransferInfo(order)),
                _ => null,
            };

            if (successOutcome is not null
                && !await _idempotencyStore.SetPendingOutcomeAsync(
                    claim.Record.Id,
                    claim.LeaseOwnerId,
                    successOutcome,
                    cancellationToken))
            {
                return CheckoutExecutionResult.InProgress();
            }

            var reservationStatus = paymentKind == CheckoutPaymentKind.CashOnDelivery
                ? InventoryReservationStatus.Consumed
                : InventoryReservationStatus.Reserved;
            var inventoryResult = await _inventoryReservationService.CreateOrderWithInventoryAsync(
                order,
                reservationStatus,
                claim.Record.Id,
                cancellationToken);
            if (!inventoryResult.Success)
            {
                return await FinishFailureAsync(
                    claim,
                    inventoryResult.ErrorMessage ?? "Unable to reserve the requested inventory.",
                    cancellationToken);
            }

            if (paymentKind == CheckoutPaymentKind.Stripe)
            {
                return await InitializeStripeAsync(order, claim, cancellationToken);
            }

            return await CompleteLocalPaymentAsync(
                paymentKind.Value,
                customer,
                claim,
                successOutcome!,
                cancellationToken);
        }

        private async Task<CheckoutExecutionResult> ResumeCommittedOrderAsync(
            Order order,
            CheckoutPaymentKind paymentKind,
            AppUser customer,
            CheckoutIdempotencyClaim claim,
            CancellationToken cancellationToken)
        {
            if (paymentKind == CheckoutPaymentKind.Stripe)
            {
                return await InitializeStripeAsync(order, claim, cancellationToken);
            }

            var outcome = _idempotencyStore.ReadOutcome(claim.Record)
                ?? (paymentKind == CheckoutPaymentKind.CashOnDelivery
                    ? CreateSuccessOutcome(
                        order,
                        CheckoutStatus.Confirmed,
                        paymentKind,
                        "Order placed with Cash on Delivery. You will pay upon delivery.")
                    : CreateSuccessOutcome(
                        order,
                        CheckoutStatus.PendingPayment,
                        paymentKind,
                        "Order placed. Complete the bank transfer using the supplied instructions.",
                        bankTransfer: BuildBankTransferInfo(order)));
            return await CompleteLocalPaymentAsync(
                paymentKind,
                customer,
                claim,
                outcome,
                cancellationToken);
        }

        private async Task<CheckoutExecutionResult> CompleteLocalPaymentAsync(
            CheckoutPaymentKind paymentKind,
            AppUser customer,
            CheckoutIdempotencyClaim claim,
            PersistedCheckoutOutcome outcome,
            CancellationToken cancellationToken)
        {
            var completed = await _idempotencyStore.CompleteAsync(
                claim.Record.Id,
                claim.LeaseOwnerId,
                outcome,
                CheckoutIdempotencyState.Completed,
                cancellationToken);
            if (!completed)
            {
                return CheckoutExecutionResult.InProgress();
            }

            if (paymentKind == CheckoutPaymentKind.BankTransfer
                && outcome.Response.Payload?.BankTransfer is not null)
            {
                await TrySendBankTransferEmailAsync(customer, outcome.Response.Payload.BankTransfer);
            }

            return FromPersistedOutcome(outcome);
        }

        private async Task<CheckoutExecutionResult> InitializeStripeAsync(
            Order order,
            CheckoutIdempotencyClaim claim,
            CancellationToken cancellationToken)
        {
            PaymentTransaction paymentTransaction;
            try
            {
                paymentTransaction = await _paymentTransactionStore.GetOrCreateStripeAsync(
                    order.Id,
                    CurrencyMoney.ToMinorUnits(order.TotalAmount, order.Currency),
                    order.Currency,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is InvalidOperationException
                or ArgumentOutOfRangeException
                or OverflowException)
            {
                _logger.LogError(
                    exception,
                    "Could not create or recover the Stripe payment transaction for order {OrderId}.",
                    order.Id);
                await _idempotencyStore.ReleaseLeaseAsync(
                    claim.Record.Id,
                    claim.LeaseOwnerId,
                    cancellationToken);
                return CheckoutExecutionResult.InProgress();
            }

            var terminalResult = await FinalizeExistingStripeStateAsync(
                order,
                paymentTransaction,
                claim,
                cancellationToken);
            if (terminalResult is not null)
            {
                return terminalResult;
            }

            var providerInitialization = await _idempotencyStore.PrepareProviderInitializationAsync(
                claim.Record.Id,
                claim.LeaseOwnerId,
                CreateStripeInitialization(order, paymentTransaction),
                cancellationToken);
            if (providerInitialization is null)
            {
                return CheckoutExecutionResult.InProgress();
            }

            var initialization = providerInitialization.Initialization;
            if (initialization.OrderId != order.Id
                || initialization.PaymentTransactionId != paymentTransaction.Id
                || initialization.ExpectedAmountMinor != paymentTransaction.ExpectedAmountMinor
                || !string.Equals(initialization.Currency, paymentTransaction.Currency, StringComparison.Ordinal)
                || !string.Equals(initialization.OrderReference, order.Reference, StringComparison.Ordinal))
            {
                var invalidSnapshot = CreateFailureOutcome(
                    "Card payment initialization could not be recovered safely. Start a new checkout attempt.");
                if (!await _idempotencyStore.SetPendingOutcomeAsync(
                    claim.Record.Id,
                    claim.LeaseOwnerId,
                    invalidSnapshot,
                    cancellationToken))
                {
                    return CheckoutExecutionResult.InProgress();
                }

                return await TerminalizeStripeAsync(
                    order,
                    paymentTransaction,
                    claim,
                    invalidSnapshot,
                    PaymentTransactionStatus.Failed,
                    cancellationToken);
            }

            if (DateTime.UtcNow >= providerInitialization.StartedOn
                .AddHours(_idempotencyOptions.ProviderRecoveryWindowHours))
            {
                var expired = CreateFailureOutcome(
                    "The safe card-payment recovery window expired. Start a new checkout with a new Idempotency-Key.");
                if (!await _idempotencyStore.SetPendingOutcomeAsync(
                    claim.Record.Id,
                    claim.LeaseOwnerId,
                    expired,
                    cancellationToken))
                {
                    return CheckoutExecutionResult.InProgress();
                }

                return await TerminalizeStripeAsync(
                    order,
                    paymentTransaction,
                    claim,
                    expired,
                    PaymentTransactionStatus.Cancelled,
                    cancellationToken);
            }

            var pendingFailure = _idempotencyStore.ReadOutcome(claim.Record);
            if (pendingFailure?.Status == CheckoutExecutionStatus.BadRequest)
            {
                return await TerminalizeStripeAsync(
                    order,
                    paymentTransaction,
                    claim,
                    pendingFailure,
                    PaymentTransactionStatus.Failed,
                    cancellationToken);
            }

            var paymentResult = await _paymentService.Pay(
                initialization,
                $"blazorshop-checkout-{claim.Record.Id:N}",
                cancellationToken);

            if (paymentResult.Success
                && !string.IsNullOrWhiteSpace(paymentResult.RedirectUrl)
                && !string.IsNullOrWhiteSpace(paymentResult.ProviderSessionId))
            {
                PaymentProviderIdentityPersistenceResult identityResult;
                try
                {
                    identityResult = await _paymentTransactionStore.PersistProviderIdentityAsync(
                        paymentTransaction.Id,
                        paymentResult.ProviderSessionId,
                        paymentResult.ProviderPaymentIntentId,
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Stripe returned session {ProviderSessionId} for transaction {PaymentTransactionId}, but provider identity persistence was ambiguous.",
                        paymentResult.ProviderSessionId,
                        paymentTransaction.Id);
                    await _idempotencyStore.ReleaseLeaseAsync(
                        claim.Record.Id,
                        claim.LeaseOwnerId,
                        cancellationToken);
                    return CheckoutExecutionResult.InProgress();
                }

                if (identityResult.Outcome is not (PaymentProviderIdentityPersistenceOutcome.Persisted
                    or PaymentProviderIdentityPersistenceOutcome.AlreadyPersisted))
                {
                    _logger.LogWarning(
                        "Stripe provider identity for transaction {PaymentTransactionId} could not be persisted safely: {Outcome}.",
                        paymentTransaction.Id,
                        identityResult.Outcome);
                    await _idempotencyStore.ReleaseLeaseAsync(
                        claim.Record.Id,
                        claim.LeaseOwnerId,
                        cancellationToken);
                    return CheckoutExecutionResult.InProgress();
                }

                if (identityResult.PaymentStatus == PaymentTransactionStatus.Paid)
                {
                    return await FinalizePaidStripeCheckoutAsync(order, claim, cancellationToken);
                }

                if (identityResult.PaymentStatus is PaymentTransactionStatus.Failed
                    or PaymentTransactionStatus.Cancelled)
                {
                    return await FinalizeTerminalStripeFailureAsync(
                        identityResult.PaymentStatus.Value,
                        claim,
                        cancellationToken);
                }

                if (identityResult.PaymentStatus != PaymentTransactionStatus.Pending)
                {
                    await _idempotencyStore.ReleaseLeaseAsync(
                        claim.Record.Id,
                        claim.LeaseOwnerId,
                        cancellationToken);
                    return CheckoutExecutionResult.InProgress();
                }

                var outcome = CreateSuccessOutcome(
                    order,
                    CheckoutStatus.PendingPayment,
                    CheckoutPaymentKind.Stripe,
                    "Order created. Continue to the secure card payment page.",
                    paymentResult.RedirectUrl);
                var completed = await _idempotencyStore.CompleteAsync(
                    claim.Record.Id,
                    claim.LeaseOwnerId,
                    outcome,
                    CheckoutIdempotencyState.Completed,
                    cancellationToken);
                return completed
                    ? FromPersistedOutcome(outcome)
                    : CheckoutExecutionResult.InProgress();
            }

            if (paymentResult.FailureKind == PaymentInitializationFailureKind.Ambiguous)
            {
                await _idempotencyStore.ReleaseLeaseAsync(
                    claim.Record.Id,
                    claim.LeaseOwnerId,
                    cancellationToken);
                return CheckoutExecutionResult.InProgress();
            }

            var failure = CreateFailureOutcome(
                paymentResult.ErrorMessage
                ?? "Unable to initialize the card payment session. Please start a new checkout attempt.");
            if (!await _idempotencyStore.SetPendingOutcomeAsync(
                claim.Record.Id,
                claim.LeaseOwnerId,
                failure,
                cancellationToken))
            {
                return CheckoutExecutionResult.InProgress();
            }

            return await TerminalizeStripeAsync(
                order,
                paymentTransaction,
                claim,
                failure,
                PaymentTransactionStatus.Failed,
                cancellationToken);
        }

        private async Task<CheckoutExecutionResult> TerminalizeStripeAsync(
            Order order,
            PaymentTransaction paymentTransaction,
            CheckoutIdempotencyClaim claim,
            PersistedCheckoutOutcome failure,
            PaymentTransactionStatus targetStatus,
            CancellationToken cancellationToken)
        {
            var transition = await _stripePaymentStateTransitionService.TransitionAsync(
                paymentTransaction.Id,
                targetStatus,
                cancellationToken);

            if (transition.PaymentStatus == PaymentTransactionStatus.Paid)
            {
                return await FinalizePaidStripeCheckoutAsync(order, claim, cancellationToken);
            }

            if (transition.PaymentStatus is PaymentTransactionStatus.Failed
                or PaymentTransactionStatus.Cancelled)
            {
                return await FinalizeTerminalStripeFailureAsync(
                    transition.PaymentStatus.Value,
                    claim,
                    cancellationToken,
                    failure);
            }

            _logger.LogWarning(
                "Could not terminalize Stripe transaction {PaymentTransactionId} as {TargetStatus}: {Outcome}, current state {PaymentStatus}, reason {Reason}",
                paymentTransaction.Id,
                targetStatus,
                transition.Outcome,
                transition.PaymentStatus,
                transition.ErrorMessage);
            await _idempotencyStore.ReleaseLeaseAsync(
                claim.Record.Id,
                claim.LeaseOwnerId,
                cancellationToken);
            return CheckoutExecutionResult.InProgress();
        }

        private async Task<CheckoutExecutionResult?> FinalizeExistingStripeStateAsync(
            Order order,
            PaymentTransaction paymentTransaction,
            CheckoutIdempotencyClaim claim,
            CancellationToken cancellationToken)
        {
            if (paymentTransaction.Status == PaymentTransactionStatus.Pending)
            {
                return null;
            }

            if (paymentTransaction.Status == PaymentTransactionStatus.Paid)
            {
                return await FinalizePaidStripeCheckoutAsync(order, claim, cancellationToken);
            }

            return await FinalizeTerminalStripeFailureAsync(
                paymentTransaction.Status,
                claim,
                cancellationToken);
        }

        private async Task<CheckoutExecutionResult> FinalizePaidStripeCheckoutAsync(
            Order order,
            CheckoutIdempotencyClaim claim,
            CancellationToken cancellationToken)
        {
            var paidOutcome = CreateSuccessOutcome(
                order,
                CheckoutStatus.Confirmed,
                CheckoutPaymentKind.Stripe,
                "Card payment was already confirmed for this order.");
            var completed = await _idempotencyStore.CompleteAsync(
                claim.Record.Id,
                claim.LeaseOwnerId,
                paidOutcome,
                CheckoutIdempotencyState.Completed,
                cancellationToken);
            return completed
                ? FromPersistedOutcome(paidOutcome)
                : CheckoutExecutionResult.InProgress();
        }

        private async Task<CheckoutExecutionResult> FinalizeTerminalStripeFailureAsync(
            PaymentTransactionStatus paymentStatus,
            CheckoutIdempotencyClaim claim,
            CancellationToken cancellationToken,
            PersistedCheckoutOutcome? existingFailure = null)
        {
            var failure = existingFailure
                ?? _idempotencyStore.ReadOutcome(claim.Record)
                ?? CreateFailureOutcome(paymentStatus == PaymentTransactionStatus.Cancelled
                    ? "The card-payment attempt expired or was cancelled. Start a new checkout attempt."
                    : "Unable to initialize the card payment session. Please start a new checkout attempt.");
            var completed = await _idempotencyStore.CompleteAsync(
                claim.Record.Id,
                claim.LeaseOwnerId,
                failure,
                CheckoutIdempotencyState.Failed,
                cancellationToken);
            return completed
                ? FromPersistedOutcome(failure)
                : CheckoutExecutionResult.InProgress();
        }

        private async Task<CheckoutExecutionResult> FinishFailureAsync(
            CheckoutIdempotencyClaim claim,
            string message,
            CancellationToken cancellationToken)
        {
            var outcome = CreateFailureOutcome(message);
            var completed = await _idempotencyStore.CompleteAsync(
                claim.Record.Id,
                claim.LeaseOwnerId,
                outcome,
                CheckoutIdempotencyState.Failed,
                cancellationToken);
            return completed
                ? FromPersistedOutcome(outcome)
                : CheckoutExecutionResult.InProgress();
        }

        private async Task<CheckoutPaymentKind?> ResolvePaymentKindAsync(Guid paymentMethodId)
        {
            var availableMethods = await _paymentMethodService.GetPaymentMethodsAsync();
            if (!availableMethods.Any(method => method.Id == paymentMethodId))
            {
                return null;
            }

            return paymentMethodId switch
            {
                var id when id == PaymentMethodIds.CreditCard => CheckoutPaymentKind.Stripe,
                var id when id == PaymentMethodIds.CashOnDelivery => CheckoutPaymentKind.CashOnDelivery,
                var id when id == PaymentMethodIds.BankTransfer => CheckoutPaymentKind.BankTransfer,
                _ => null,
            };
        }

        private async Task<CartLineResolution> ResolveCartLinesAsync(IEnumerable<CartLineRequest> carts)
        {
            var cartList = carts?.ToList() ?? [];
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

                    if (variant.Stock <= 0 || line.Quantity > variant.Stock)
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

                    if (product.Quantity <= 0 || line.Quantity > product.Quantity)
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

        private static Order CreateOrder(
            IReadOnlyCollection<ResolvedCartLine> lines,
            string userId,
            Guid orderId,
            string orderReference,
            CheckoutPaymentKind paymentKind,
            string currency)
        {
            var normalizedCurrency = CurrencyMoney.NormalizeCurrency(currency);
            var orderLines = lines.Select(line =>
            {
                var unitPrice = CurrencyMoney.NormalizeAmount(line.UnitPrice, normalizedCurrency);
                var lineTotal = checked(unitPrice * line.Quantity);
                return new OrderLine
                {
                    ProductId = line.ProductId,
                    ProductVariantId = line.VariantId,
                    ProductNameSnapshot = line.ProductName,
                    SkuSnapshot = line.Sku,
                    SizeScaleSnapshot = line.SizeScale?.ToString(),
                    SizeValueSnapshot = line.SizeValue,
                    ColorSnapshot = line.Color,
                    Quantity = line.Quantity,
                    UnitPrice = unitPrice,
                    LineTotal = lineTotal,
                };
            }).ToList();
            var totalAmount = orderLines.Aggregate(0m, (total, line) => checked(total + line.LineTotal));
            _ = CurrencyMoney.ToMinorUnits(totalAmount, normalizedCurrency);
            return new Order
            {
                Id = orderId,
                UserId = userId,
                Status = paymentKind == CheckoutPaymentKind.Stripe
                    ? PaymentOrderStatus.PendingPayment
                    : "Pending",
                Reference = orderReference,
                TotalAmount = totalAmount,
                Currency = normalizedCurrency,
                Lines = orderLines,
            };
        }

        private StripeCheckoutInitialization CreateStripeInitialization(
            Order order,
            PaymentTransaction paymentTransaction)
        {
            var lines = order.Lines
                .OrderBy(line => line.ProductId)
                .ThenBy(line => line.ProductVariantId)
                .Select(line => new StripeCheckoutLineItem(
                    line.ProductId,
                    line.ProductVariantId,
                    line.ProductNameSnapshot,
                    BuildStripeDescription(line),
                    line.Quantity,
                    CurrencyMoney.ToMinorUnits(line.UnitPrice, order.Currency),
                    order.Currency.ToLowerInvariant()))
                .ToArray();
            var stripeTotal = lines.Aggregate(
                0L,
                (total, line) => checked(total + checked(line.UnitAmount * line.Quantity)));
            if (stripeTotal != paymentTransaction.ExpectedAmountMinor)
            {
                throw new InvalidOperationException(
                    "The Stripe line total does not match the immutable payment transaction amount.");
            }

            return new StripeCheckoutInitialization(
                StripeInitializationVersion,
                order.Id,
                order.Reference,
                paymentTransaction.Id,
                paymentTransaction.ExpectedAmountMinor,
                paymentTransaction.Currency,
                ["card"],
                "payment",
                lines,
                BuildClientUrl(
                    $"payment-success?pm=card&order_id={order.Id:D}&reference={Uri.EscapeDataString(order.Reference)}&session_id={{CHECKOUT_SESSION_ID}}"),
                BuildClientUrl($"payment-cancel?order_id={order.Id:D}"));
        }

        private static string? BuildStripeDescription(OrderLine line)
        {
            var details = new[]
                {
                    string.IsNullOrWhiteSpace(line.SkuSnapshot) ? null : $"SKU: {line.SkuSnapshot}",
                    string.IsNullOrWhiteSpace(line.SizeValueSnapshot)
                        ? null
                        : string.IsNullOrWhiteSpace(line.SizeScaleSnapshot)
                            ? $"Size: {line.SizeValueSnapshot}"
                            : $"Size: {line.SizeScaleSnapshot} {line.SizeValueSnapshot}",
                    string.IsNullOrWhiteSpace(line.ColorSnapshot) ? null : $"Color: {line.ColorSnapshot}",
                }
                .Where(value => value is not null)
                .ToArray();
            return details.Length == 0 ? null : string.Join(", ", details);
        }

        private string BuildClientUrl(string path) =>
            $"{_clientAppOptions.BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";

        private BankTransferInfo BuildBankTransferInfo(Order order) => new()
        {
            Iban = string.IsNullOrWhiteSpace(_bankTransferSettings.Iban)
                ? "BG00UNCR70001512345678"
                : _bankTransferSettings.Iban,
            Beneficiary = _bankTransferSettings.Beneficiary,
            BankName = _bankTransferSettings.BankName,
            Reference = order.Reference,
            Amount = order.TotalAmount,
            Currency = order.Currency,
            AdditionalInfo = _bankTransferSettings.AdditionalInfo,
        };

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
<li>Amount: <b>{info.Amount:F2} {info.Currency}</b></li>
<li>Reference: <b>{info.Reference}</b></li>
</ul>
<p>{info.AdditionalInfo}</p>
<p>Your order will be processed once we receive the payment.</p>";
                await _emailService.SendEmailAsync(customer.Email, "Bank Transfer Instructions", html);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Order {OrderReference} was created, but its bank-transfer email could not be sent.",
                    info.Reference);
            }
        }

        private static PersistedCheckoutOutcome CreateSuccessOutcome(
            Order order,
            CheckoutStatus status,
            CheckoutPaymentKind paymentKind,
            string message,
            string? redirectUrl = null,
            BankTransferInfo? bankTransfer = null) => new(
                OutcomeVersion,
                CheckoutExecutionStatus.Succeeded,
                new ServiceResponse<CheckoutResult>(true, message, order.Id)
                {
                    Payload = new CheckoutResult(
                        order.Id,
                        order.Reference,
                        status,
                        paymentKind,
                        redirectUrl,
                        bankTransfer),
                    ResponseType = ServiceResponseType.Success,
                });

        private static PersistedCheckoutOutcome CreateFailureOutcome(string message) => new(
            OutcomeVersion,
            CheckoutExecutionStatus.BadRequest,
            new ServiceResponse<CheckoutResult>(false, message)
            {
                ResponseType = ServiceResponseType.ValidationError,
            });

        private static CheckoutExecutionResult FromPersistedOutcome(
            PersistedCheckoutOutcome outcome,
            bool isReplay = false) => outcome.Status switch
            {
                CheckoutExecutionStatus.Succeeded => CheckoutExecutionResult.Succeeded(outcome.Response, isReplay),
                CheckoutExecutionStatus.BadRequest => CheckoutExecutionResult.Invalid(outcome.Response, isReplay),
                _ => throw new InvalidOperationException("Only terminal checkout outcomes may be persisted."),
            };

        private async Task<CheckoutExecutionResult> ReplayPersistedOutcomeAsync(
            PersistedCheckoutOutcome outcome,
            CancellationToken cancellationToken)
        {
            var checkout = outcome.Response.Payload;
            if (checkout?.PaymentKind != CheckoutPaymentKind.Stripe)
            {
                return FromPersistedOutcome(outcome, isReplay: true);
            }

            var paymentTransaction = await _paymentTransactionStore.GetStripeByOrderIdAsync(
                checkout.OrderId,
                cancellationToken);
            if (paymentTransaction?.Status == PaymentTransactionStatus.Paid)
            {
                var paidOutcome = new PersistedCheckoutOutcome(
                    OutcomeVersion,
                    CheckoutExecutionStatus.Succeeded,
                    new ServiceResponse<CheckoutResult>(
                        true,
                        "Card payment was already confirmed for this order.",
                        checkout.OrderId)
                    {
                        Payload = checkout with
                        {
                            Status = CheckoutStatus.Confirmed,
                            RedirectUrl = null,
                        },
                        ResponseType = ServiceResponseType.Success,
                    });
                return FromPersistedOutcome(paidOutcome, isReplay: true);
            }

            if (paymentTransaction?.Status is PaymentTransactionStatus.Failed
                or PaymentTransactionStatus.Cancelled)
            {
                var failure = CreateFailureOutcome(
                    paymentTransaction.Status == PaymentTransactionStatus.Cancelled
                        ? "The card-payment attempt expired or was cancelled. Start a new checkout attempt."
                        : "The card-payment attempt failed. Start a new checkout attempt.");
                return FromPersistedOutcome(failure, isReplay: true);
            }

            return FromPersistedOutcome(outcome, isReplay: true);
        }

        private static CheckoutExecutionResult BadRequest(string message) =>
            CheckoutExecutionResult.Invalid(CreateFailureOutcome(message).Response);

        private sealed record CartLineResolution(
            IReadOnlyList<ResolvedCartLine> Lines,
            string? ErrorMessage)
        {
            public static CartLineResolution Success(IReadOnlyList<ResolvedCartLine> lines) => new(lines, null);

            public static CartLineResolution Failure(string message) => new([], message);
        }
    }
}
