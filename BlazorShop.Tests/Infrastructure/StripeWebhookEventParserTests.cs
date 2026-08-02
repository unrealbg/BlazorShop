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
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = $$"""
                {
                  "id": "evt_test",
                  "object": "event",
                  "api_version": "{{StripeConfiguration.ApiVersion}}",
                  "request": null,
                  "type": "checkout.session.completed",
                  "data": {
                    "object": {
                      "id": "cs_test",
                      "object": "checkout.session",
                      "payment_status": "paid",
                      "metadata": {
                        "order_id": "{{orderId:D}}"
                      }
                    }
                  }
                }
                """;
            var signature = CreateSignature(payload, secret, timestamp);

            var result = new StripeWebhookEventParser().Parse(payload, signature, secret);

            Assert.Equal("evt_test", result.EventId);
            Assert.Equal("checkout.session.completed", result.EventType);
            Assert.Equal(orderId, result.OrderId);
            Assert.Equal("paid", result.PaymentStatus);
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
