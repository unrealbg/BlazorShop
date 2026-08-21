namespace BlazorShop.Infrastructure.Services
{
    using BlazorShop.Application.Options;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public sealed class StripeWebhookService : IStripeWebhookService
    {
        private const string CheckoutCompleted = "checkout.session.completed";
        private const string CheckoutAsyncPaymentSucceeded = "checkout.session.async_payment_succeeded";
        private const string CheckoutAsyncPaymentFailed = "checkout.session.async_payment_failed";
        private const string CheckoutExpired = "checkout.session.expired";

        private readonly IStripeWebhookEventParser _eventParser;
        private readonly IStripePaymentReconciliationService _reconciliationService;
        private readonly StripeOptions _options;
        private readonly ILogger<StripeWebhookService> _logger;

        public StripeWebhookService(
            IStripeWebhookEventParser eventParser,
            IStripePaymentReconciliationService reconciliationService,
            IOptions<StripeOptions> options,
            ILogger<StripeWebhookService> logger)
        {
            _eventParser = eventParser;
            _reconciliationService = reconciliationService;
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

            if (!IsHandledEventType(stripeEvent.EventType))
            {
                _logger.LogDebug(
                    "Ignoring Stripe event {StripeEventId} of type {StripeEventType}.",
                    stripeEvent.EventId,
                    stripeEvent.EventType);
                return StripeWebhookHandlingResult.Ignored;
            }

            try
            {
                return await _reconciliationService.ReconcileAsync(stripeEvent, cancellationToken) switch
                {
                    StripePaymentReconciliationOutcome.Processed => StripeWebhookHandlingResult.Processed,
                    StripePaymentReconciliationOutcome.Duplicate => StripeWebhookHandlingResult.Duplicate,
                    StripePaymentReconciliationOutcome.Rejected => StripeWebhookHandlingResult.Rejected,
                    StripePaymentReconciliationOutcome.Ignored => StripeWebhookHandlingResult.Ignored,
                    _ => throw new InvalidOperationException("Unknown Stripe reconciliation outcome."),
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Stripe event {StripeEventId} failed transiently and remains eligible for provider retry.",
                    stripeEvent.EventId);
                return StripeWebhookHandlingResult.TransientFailure;
            }
        }

        private static bool IsHandledEventType(string eventType) =>
            eventType is CheckoutCompleted
                or CheckoutAsyncPaymentSucceeded
                or CheckoutAsyncPaymentFailed
                or CheckoutExpired;
    }
}
