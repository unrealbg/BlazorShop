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
            if (!HasValidAuthoritativeTotal(initialization))
            {
                return new PaymentInitializationResult(
                    false,
                    ErrorMessage: "The card payment amount does not match the immutable order total.",
                    FailureKind: PaymentInitializationFailureKind.Definitive);
            }

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
                        ["payment_transaction_id"] = initialization.PaymentTransactionId.ToString("D"),
                    },
                    PaymentIntentData = new SessionPaymentIntentDataOptions
                    {
                        Metadata = new Dictionary<string, string>
                        {
                            ["order_id"] = initialization.OrderId.ToString("D"),
                            ["payment_transaction_id"] = initialization.PaymentTransactionId.ToString("D"),
                        },
                    },
                    SuccessUrl = initialization.SuccessUrl,
                    CancelUrl = initialization.CancelUrl,
                };

                var session = await _checkoutSessionService.CreateAsync(
                    opt,
                    providerIdempotencyKey,
                    cancellationToken);

                return new PaymentInitializationResult(
                    true,
                    session.Url,
                    ProviderSessionId: session.Id,
                    ProviderPaymentIntentId: session.PaymentIntentId);
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

        public async Task<StripePaymentRecoveryResult> RecoverAsync(
            StripePaymentRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!HasValidAuthoritativeTotal(request.Initialization)
                || string.IsNullOrWhiteSpace(request.ProviderSessionId))
            {
                return Unresolved("The stored Stripe recovery identity is incomplete or invalid.");
            }

            var session = await TryGetSessionAsync(request.ProviderSessionId, cancellationToken);
            if (session is null)
            {
                return Unresolved("Stripe session state could not be confirmed.");
            }

            var inspected = InspectRecoverySession(request, session);
            if (inspected.Outcome != StripePaymentRecoveryOutcome.Open)
            {
                return inspected;
            }

            try
            {
                await _checkoutSessionService.ExpireAsync(request.ProviderSessionId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Stripe session {ProviderSessionId} could not be expired conclusively; re-reading provider state.",
                    request.ProviderSessionId);
            }

            // Expiration can race payment completion, and a successful API response is not the
            // authoritative final observation. Always re-read before returning a terminal outcome.
            session = await TryGetSessionAsync(request.ProviderSessionId, cancellationToken);
            return session is null
                ? Unresolved("Stripe session state remained unavailable after the expiration attempt.")
                : InspectRecoverySession(request, session);
        }

        private async Task<Session?> TryGetSessionAsync(
            string providerSessionId,
            CancellationToken cancellationToken)
        {
            try
            {
                return await _checkoutSessionService.GetAsync(providerSessionId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Stripe session {ProviderSessionId} could not be read for checkout recovery.",
                    providerSessionId);
                return null;
            }
        }

        private static StripePaymentRecoveryResult InspectRecoverySession(
            StripePaymentRecoveryRequest request,
            Session session)
        {
            var initialization = request.Initialization;
            if (!string.Equals(session.Id, request.ProviderSessionId, StringComparison.Ordinal)
                || !string.Equals(
                    session.ClientReferenceId,
                    initialization.OrderId.ToString("D"),
                    StringComparison.OrdinalIgnoreCase)
                || !HasMetadataIdentity(session.Metadata, "order_id", initialization.OrderId)
                || !HasMetadataIdentity(
                    session.Metadata,
                    "payment_transaction_id",
                    initialization.PaymentTransactionId)
                || session.AmountTotal != initialization.ExpectedAmountMinor
                || !string.Equals(session.Currency, initialization.Currency, StringComparison.OrdinalIgnoreCase)
                || (request.ProviderPaymentIntentId is not null
                    && !string.Equals(
                        session.PaymentIntentId,
                        request.ProviderPaymentIntentId,
                        StringComparison.Ordinal)))
            {
                return Unresolved("Stripe session identity, amount, or currency contradicted the local checkout snapshot.");
            }

            if (string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase)
                || (string.Equals(
                        session.PaymentStatus,
                        "no_payment_required",
                        StringComparison.OrdinalIgnoreCase)
                    && initialization.ExpectedAmountMinor == 0))
            {
                return new StripePaymentRecoveryResult(
                    StripePaymentRecoveryOutcome.Paid,
                    session.PaymentIntentId);
            }

            if (string.Equals(session.Status, "expired", StringComparison.OrdinalIgnoreCase)
                && string.Equals(session.PaymentStatus, "unpaid", StringComparison.OrdinalIgnoreCase))
            {
                return new StripePaymentRecoveryResult(
                    StripePaymentRecoveryOutcome.Expired,
                    session.PaymentIntentId);
            }

            if (string.Equals(session.Status, "open", StringComparison.OrdinalIgnoreCase))
            {
                return new StripePaymentRecoveryResult(
                    StripePaymentRecoveryOutcome.Open,
                    session.PaymentIntentId,
                    "The Stripe session remains open and payable.");
            }

            return Unresolved(
                "Stripe did not report a safely terminal paid or expired/unpaid session state.",
                session.PaymentIntentId);
        }

        private static bool HasMetadataIdentity(
            IReadOnlyDictionary<string, string>? metadata,
            string key,
            Guid expected) =>
            metadata is not null
            && metadata.TryGetValue(key, out var value)
            && Guid.TryParse(value, out var parsed)
            && parsed == expected;

        private static StripePaymentRecoveryResult Unresolved(
            string reason,
            string? providerPaymentIntentId = null) =>
            new(StripePaymentRecoveryOutcome.Unresolved, providerPaymentIntentId, reason);

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

        private static bool HasValidAuthoritativeTotal(StripeCheckoutInitialization initialization)
        {
            if (initialization.PaymentTransactionId == Guid.Empty
                || initialization.OrderId == Guid.Empty
                || initialization.ExpectedAmountMinor < 0
                || string.IsNullOrWhiteSpace(initialization.Currency)
                || initialization.Lines.Count == 0
                || initialization.Lines.Any(line =>
                    line.Quantity <= 0
                    || line.UnitAmount < 0
                    || !string.Equals(line.Currency, initialization.Currency, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            try
            {
                var lineTotal = initialization.Lines.Aggregate(
                    0L,
                    (total, line) => checked(total + checked(line.UnitAmount * line.Quantity)));
                return lineTotal == initialization.ExpectedAmountMinor;
            }
            catch (OverflowException)
            {
                return false;
            }
        }
    }
}
