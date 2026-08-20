namespace BlazorShop.Tests.Presentation.Payments
{
    using System.Text.Json;

    using ApiCheckoutPaymentKind = BlazorShop.Application.DTOs.Payment.CheckoutPaymentKind;
    using ApiCheckoutResult = BlazorShop.Application.DTOs.Payment.CheckoutResult;
    using ApiCheckoutStatus = BlazorShop.Application.DTOs.Payment.CheckoutStatus;
    using ClientCheckoutPaymentKind = BlazorShop.Web.Shared.Models.Payment.CheckoutPaymentKind;
    using ClientCheckoutResult = BlazorShop.Web.Shared.Models.Payment.CheckoutResult;
    using ClientCheckoutStatus = BlazorShop.Web.Shared.Models.Payment.CheckoutStatus;

    using Xunit;

    public sealed class CheckoutResultContractTests
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public void BankTransferResult_RoundTripsAsTypedClientContract()
        {
            var orderId = Guid.NewGuid();
            var apiResponse = new BlazorShop.Application.DTOs.ServiceResponse<ApiCheckoutResult>(true)
            {
                Payload = new ApiCheckoutResult(
                    orderId,
                    "BT-TEST",
                    ApiCheckoutStatus.PendingPayment,
                    ApiCheckoutPaymentKind.BankTransfer,
                    BankTransfer: new BlazorShop.Application.DTOs.Payment.BankTransferInfo
                    {
                        Iban = "BG00TEST",
                        Beneficiary = "Blazor Shop",
                        BankName = "Test Bank",
                        Reference = "BT-TEST",
                        Amount = 42m,
                    }),
            };

            var json = JsonSerializer.Serialize(apiResponse, SerializerOptions);
            var clientResponse = JsonSerializer.Deserialize<
                BlazorShop.Web.Shared.Models.ServiceResponse<ClientCheckoutResult>>(
                json,
                SerializerOptions);

            Assert.NotNull(clientResponse?.Payload);
            Assert.Equal(orderId, clientResponse.Payload!.OrderId);
            Assert.Equal(ClientCheckoutStatus.PendingPayment, clientResponse.Payload.Status);
            Assert.Equal(ClientCheckoutPaymentKind.BankTransfer, clientResponse.Payload.PaymentKind);
            Assert.Equal("BG00TEST", clientResponse.Payload.BankTransfer!.Iban);
            Assert.Equal(42m, clientResponse.Payload.BankTransfer.Amount);
        }

        [Fact]
        public void StripeResult_RoundTripsTypedRedirectWithoutUsingMessage()
        {
            var apiResponse = new BlazorShop.Application.DTOs.ServiceResponse<ApiCheckoutResult>(
                true,
                "Human-readable status")
            {
                Payload = new ApiCheckoutResult(
                    Guid.NewGuid(),
                    "STRIPE-TEST",
                    ApiCheckoutStatus.PendingPayment,
                    ApiCheckoutPaymentKind.Stripe,
                    "https://checkout.stripe.test/session"),
            };

            var json = JsonSerializer.Serialize(apiResponse, SerializerOptions);
            var clientResponse = JsonSerializer.Deserialize<
                BlazorShop.Web.Shared.Models.ServiceResponse<ClientCheckoutResult>>(
                json,
                SerializerOptions);

            Assert.NotNull(clientResponse?.Payload);
            Assert.Equal(ClientCheckoutPaymentKind.Stripe, clientResponse.Payload!.PaymentKind);
            Assert.Equal("https://checkout.stripe.test/session", clientResponse.Payload.RedirectUrl);
            Assert.Equal("Human-readable status", clientResponse.Message);
            Assert.NotEqual(clientResponse.Message, clientResponse.Payload.RedirectUrl);
        }
    }
}
