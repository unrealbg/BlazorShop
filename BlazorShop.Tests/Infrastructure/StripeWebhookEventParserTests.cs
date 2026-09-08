namespace BlazorShop.Tests.Infrastructure
{
    using System.Security.Cryptography;
    using System.Text;

    using BlazorShop.Infrastructure.Services;

    using Stripe;

    using Xunit;

    public sealed class StripeWebhookEventParserTests
    {
        [Fact]
        public void Parse_ValidSignedCheckoutEvent_ReturnsTrustedOrderMetadata()
        {
            const string secret = "whsec_test";
            var orderId = Guid.NewGuid();
            var paymentTransactionId = Guid.NewGuid();
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = $$"""
                {
                  "id": "evt_test",
                  "object": "event",
                  "api_version": "{{StripeConfiguration.ApiVersion}}",
                  "request": null,
                  "created": {{timestamp}},
                  "type": "checkout.session.completed",
                  "data": {
                    "object": {
                      "id": "cs_test",
                      "object": "checkout.session",
                      "amount_total": 12345,
                      "client_reference_id": "{{orderId:D}}",
                      "currency": "eur",
                      "payment_intent": "pi_test",
                      "payment_status": "paid",
                      "status": "complete",
                      "metadata": {
                        "order_id": "{{orderId:D}}",
                        "payment_transaction_id": "{{paymentTransactionId:D}}"
                      }
                    }
                  }
                }
                """;
            var signature = CreateSignature(payload, secret, timestamp);

            var result = new StripeWebhookEventParser().Parse(payload, signature, secret);

            Assert.Equal("evt_test", result.EventId);
            Assert.Equal("checkout.session.completed", result.EventType);
            Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime, result.ProviderCreatedOn);
            Assert.Equal("cs_test", result.SessionId);
            Assert.Equal("pi_test", result.PaymentIntentId);
            Assert.Equal(orderId, result.OrderId);
            Assert.Equal(orderId.ToString("D"), result.ClientReferenceId);
            Assert.Equal(paymentTransactionId, result.PaymentTransactionId);
            Assert.Equal("paid", result.PaymentStatus);
            Assert.Equal(12345, result.AmountTotal);
            Assert.Equal("eur", result.Currency);
            Assert.Equal("complete", result.SessionStatus);
        }

        [Fact]
        public void Parse_InvalidSignature_ThrowsSafeWrapper()
        {
            var parser = new StripeWebhookEventParser();

            Assert.Throws<StripeWebhookSignatureException>(
                () => parser.Parse("{}", "t=1,v1=invalid", "whsec_test"));
        }

        private static string CreateSignature(string payload, string secret, long timestamp)
        {
            var signedPayload = $"{timestamp}.{payload}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var digest = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload)))
                .ToLowerInvariant();

            return $"t={timestamp},v1={digest}";
        }
    }
}
