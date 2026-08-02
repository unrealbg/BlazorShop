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

                if (session?.Metadata.TryGetValue("order_id", out var rawOrderId) == true
                    && Guid.TryParse(rawOrderId, out var parsedOrderId))
                {
                    orderId = parsedOrderId;
                }

                return new StripeWebhookEventData(
                    stripeEvent.Id,
                    stripeEvent.Type,
                    orderId,
                    session?.PaymentStatus);
            }
            catch (StripeException exception)
            {
                throw new StripeWebhookSignatureException(exception);
            }
        }
    }
}
