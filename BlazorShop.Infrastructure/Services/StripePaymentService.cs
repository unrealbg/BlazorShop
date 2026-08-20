namespace BlazorShop.Infrastructure.Services
{
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    using Microsoft.Extensions.Logging;

    using Stripe.Checkout;

    public class StripePaymentService : IPaymentService
    {
        private readonly IStripeCheckoutSessionService _checkoutSessionService;
        private readonly ILogger<StripePaymentService> _logger;

        public StripePaymentService(
            IStripeCheckoutSessionService checkoutSessionService,
            ILogger<StripePaymentService> logger)
        {
            _checkoutSessionService = checkoutSessionService;
            _logger = logger;
        }

        public async Task<PaymentInitializationResult> Pay(
            StripeCheckoutInitialization initialization,
            string providerIdempotencyKey,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var lineItems = new List<SessionLineItemOptions>();

                foreach (var line in initialization.Lines)
                {
                    lineItems.Add(new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = line.Currency,
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = line.ProductName,
                                Description = line.Description,
                            },
                            UnitAmount = line.UnitAmount,
                        },

                        Quantity = line.Quantity,
                    });
                }

                var opt = new SessionCreateOptions
                {
                    PaymentMethodTypes = initialization.PaymentMethodTypes.ToList(),
                    LineItems = lineItems,
                    Mode = initialization.Mode,
                    ClientReferenceId = initialization.OrderId.ToString("D"),
                    Metadata = new Dictionary<string, string>
                    {
                        ["order_id"] = initialization.OrderId.ToString("D"),
                    },
                    PaymentIntentData = new SessionPaymentIntentDataOptions
                    {
                        Metadata = new Dictionary<string, string>
                        {
                            ["order_id"] = initialization.OrderId.ToString("D"),
                        },
                    },
                    SuccessUrl = initialization.SuccessUrl,
                    CancelUrl = initialization.CancelUrl,
                };

                var session = await _checkoutSessionService.CreateAsync(
                    opt,
                    providerIdempotencyKey,
                    cancellationToken);

                return new PaymentInitializationResult(true, session.Url);
            }
            catch (Stripe.StripeException ex)
            {
                var failureKind = IsDefinitiveStripeFailure(ex)
                    ? PaymentInitializationFailureKind.Definitive
                    : PaymentInitializationFailureKind.Ambiguous;
                if (failureKind == PaymentInitializationFailureKind.Definitive)
                {
                    _logger.LogError(ex, "Stripe deterministically rejected checkout-session creation.");
                }
                else
                {
                    _logger.LogWarning(
                        ex,
                        "Stripe checkout-session creation returned a retryable or ambiguous provider failure.");
                }

                return new PaymentInitializationResult(
                    false,
                    ErrorMessage: failureKind == PaymentInitializationFailureKind.Definitive
                        ? "Unable to initialize the card payment session. Please start a new checkout attempt."
                        : "Card payment initialization is still uncertain. Retry with the same checkout key.",
                    FailureKind: failureKind);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Stripe checkout-session creation had an ambiguous transport failure.");
                return new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Card payment initialization is still uncertain. Retry with the same checkout key.",
                    FailureKind: PaymentInitializationFailureKind.Ambiguous);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Stripe checkout-session creation timed out with an ambiguous result.");
                return new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Card payment initialization is still uncertain. Retry with the same checkout key.",
                    FailureKind: PaymentInitializationFailureKind.Ambiguous);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stripe checkout-session creation ended with an ambiguous result.");
                return new PaymentInitializationResult(
                    false,
                    ErrorMessage: "Card payment initialization is still uncertain. Retry with the same checkout key.",
                    FailureKind: PaymentInitializationFailureKind.Ambiguous);
            }
        }

        private static bool IsDefinitiveStripeFailure(Stripe.StripeException exception)
        {
            var statusCode = (int)exception.HttpStatusCode;
            if (statusCode is 408 or 409 or 425 or 429 || statusCode >= 500)
            {
                return false;
            }

            var errorType = exception.StripeError?.Type;
            if (string.Equals(errorType, "api_error", StringComparison.Ordinal)
                || string.Equals(errorType, "api_connection_error", StringComparison.Ordinal)
                || string.Equals(errorType, "idempotency_error", StringComparison.Ordinal))
            {
                return false;
            }

            return statusCode is >= 400 and < 500
                && (string.Equals(errorType, "invalid_request_error", StringComparison.Ordinal)
                    || string.Equals(errorType, "authentication_error", StringComparison.Ordinal)
                    || string.Equals(errorType, "permission_error", StringComparison.Ordinal)
                    || string.Equals(errorType, "card_error", StringComparison.Ordinal));
        }
    }
}
