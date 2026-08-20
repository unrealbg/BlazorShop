namespace BlazorShop.Infrastructure.Services
{
    using BlazorShop.Application.Options;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public sealed class StripeWebhookService : IStripeWebhookService
    {
        private const string CheckoutCompleted = "checkout.session.completed";
        private const string CheckoutAsyncPaymentSucceeded = "checkout.session.async_payment_succeeded";
        private const string CheckoutAsyncPaymentFailed = "checkout.session.async_payment_failed";
        private const string CheckoutExpired = "checkout.session.expired";

        private readonly IStripeWebhookEventParser _eventParser;
        private readonly IInventoryReservationService _inventoryReservationService;
        private readonly StripeOptions _options;
        private readonly ILogger<StripeWebhookService> _logger;

        public StripeWebhookService(
            IStripeWebhookEventParser eventParser,
            IInventoryReservationService inventoryReservationService,
            IOptions<StripeOptions> options,
            ILogger<StripeWebhookService> logger)
        {
            _eventParser = eventParser;
            _inventoryReservationService = inventoryReservationService;
            _options = options.Value;
            _logger = logger;
        }

        public async Task<StripeWebhookHandlingResult> HandleAsync(
            string payload,
            string signature,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(payload)
                || string.IsNullOrWhiteSpace(signature)
                || string.IsNullOrWhiteSpace(_options.WebhookSecret))
            {
                return StripeWebhookHandlingResult.InvalidSignature;
            }

            StripeWebhookEventData stripeEvent;

            try
            {
                stripeEvent = _eventParser.Parse(payload, signature, _options.WebhookSecret);
            }
            catch (StripeWebhookSignatureException exception)
            {
                _logger.LogWarning(exception, "Rejected a Stripe webhook with an invalid signature.");
                return StripeWebhookHandlingResult.InvalidSignature;
            }

            var targetStatus = ResolveTargetStatus(stripeEvent);
            if (targetStatus is null)
            {
                _logger.LogDebug(
                    "Ignoring Stripe event {StripeEventId} of type {StripeEventType}.",
                    stripeEvent.EventId,
                    stripeEvent.EventType);
                return StripeWebhookHandlingResult.Ignored;
            }

            if (!stripeEvent.OrderId.HasValue)
            {
                _logger.LogWarning(
                    "Stripe event {StripeEventId} does not contain a valid order_id metadata value.",
                    stripeEvent.EventId);
                return StripeWebhookHandlingResult.InvalidPayload;
            }

            var reservationStatus = string.Equals(targetStatus, PaymentOrderStatus.Paid, StringComparison.Ordinal)
                ? InventoryReservationStatus.Consumed
                : InventoryReservationStatus.Released;
            var transition = await _inventoryReservationService.TransitionOrderAsync(
                stripeEvent.OrderId.Value,
                targetStatus,
                reservationStatus,
                cancellationToken);

            if (transition.Outcome == InventoryTransitionOutcome.OrderNotFound)
            {
                _logger.LogWarning(
                    "Stripe event {StripeEventId} references missing order {OrderId}.",
                    stripeEvent.EventId,
                    stripeEvent.OrderId.Value);
                return StripeWebhookHandlingResult.OrderNotFound;
            }

            if (transition.Outcome == InventoryTransitionOutcome.InvalidTransition)
            {
                _logger.LogWarning(
                    "Stripe event {StripeEventId} could not apply status {OrderStatus} to order {OrderId}: {Reason}",
                    stripeEvent.EventId,
                    targetStatus,
                    stripeEvent.OrderId.Value,
                    transition.ErrorMessage);
                return StripeWebhookHandlingResult.Processed;
            }

            _logger.LogInformation(
                "Applied Stripe event {StripeEventId} to order {OrderId}; status is now {OrderStatus}.",
                stripeEvent.EventId,
                stripeEvent.OrderId.Value,
                targetStatus);

            return StripeWebhookHandlingResult.Processed;
        }

        private static string? ResolveTargetStatus(StripeWebhookEventData stripeEvent)
        {
            return stripeEvent.EventType switch
            {
                CheckoutCompleted when IsPaid(stripeEvent.PaymentStatus) => PaymentOrderStatus.Paid,
                CheckoutAsyncPaymentSucceeded => PaymentOrderStatus.Paid,
                CheckoutAsyncPaymentFailed => PaymentOrderStatus.PaymentFailed,
                CheckoutExpired => PaymentOrderStatus.Cancelled,
                _ => null,
            };
        }

        private static bool IsPaid(string? paymentStatus)
        {
            return string.Equals(paymentStatus, "paid", StringComparison.OrdinalIgnoreCase)
                || string.Equals(paymentStatus, "no_payment_required", StringComparison.OrdinalIgnoreCase);
        }
    }
}
