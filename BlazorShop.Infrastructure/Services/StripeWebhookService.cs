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
        private readonly IOrderRepository _orderRepository;
        private readonly StripeOptions _options;
        private readonly ILogger<StripeWebhookService> _logger;

        public StripeWebhookService(
            IStripeWebhookEventParser eventParser,
            IOrderRepository orderRepository,
            IOptions<StripeOptions> options,
            ILogger<StripeWebhookService> logger)
        {
            _eventParser = eventParser;
            _orderRepository = orderRepository;
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

            var order = await _orderRepository.GetByIdAsync(stripeEvent.OrderId.Value);
            if (order is null)
            {
                _logger.LogWarning(
                    "Stripe event {StripeEventId} references missing order {OrderId}.",
                    stripeEvent.EventId,
                    stripeEvent.OrderId.Value);
                return StripeWebhookHandlingResult.OrderNotFound;
            }

            if (string.Equals(order.Status, targetStatus, StringComparison.Ordinal)
                || (string.Equals(order.Status, PaymentOrderStatus.Paid, StringComparison.Ordinal)
                    && !string.Equals(targetStatus, PaymentOrderStatus.Paid, StringComparison.Ordinal)))
            {
                return StripeWebhookHandlingResult.Processed;
            }

            await _orderRepository.UpdatePaymentStatusAsync(order.Id, targetStatus);

            _logger.LogInformation(
                "Applied Stripe event {StripeEventId} to order {OrderId}; status is now {OrderStatus}.",
                stripeEvent.EventId,
                order.Id,
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
