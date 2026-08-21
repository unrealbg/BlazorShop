namespace BlazorShop.Infrastructure.Services
{
    using Stripe;
    using Stripe.Checkout;

    public sealed class StripeWebhookEventParser : IStripeWebhookEventParser
    {
        public StripeWebhookEventData Parse(string payload, string signature, string webhookSecret)
        {
            try
            {
                var stripeEvent = EventUtility.ConstructEvent(payload, signature, webhookSecret);
                var session = stripeEvent.Data.Object as Session;
                Guid? orderId = null;
                Guid? paymentTransactionId = null;

                if (session?.Metadata.TryGetValue("order_id", out var rawOrderId) == true
                    && Guid.TryParse(rawOrderId, out var parsedOrderId))
                {
                    orderId = parsedOrderId;
                }

                if (session?.Metadata.TryGetValue(
                    "payment_transaction_id",
                    out var rawPaymentTransactionId) == true
                    && Guid.TryParse(rawPaymentTransactionId, out var parsedPaymentTransactionId))
                {
                    paymentTransactionId = parsedPaymentTransactionId;
                }

                return new StripeWebhookEventData(
                    stripeEvent.Id,
                    stripeEvent.Type,
                    stripeEvent.Created,
                    session?.Id,
                    session?.PaymentIntentId,
                    orderId,
                    session?.ClientReferenceId,
                    paymentTransactionId,
                    session?.PaymentStatus,
                    session?.AmountTotal,
                    session?.Currency,
                    session?.Status);
            }
            catch (StripeException exception)
            {
                throw new StripeWebhookSignatureException(exception);
            }
        }
    }
}
