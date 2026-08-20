namespace BlazorShop.Tests.Infrastructure.Services
{
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Infrastructure.Services;

    using Xunit;

    public class PayPalPaymentServiceTests
    {
        private readonly PayPalPaymentService _service = new();

        [Fact]
        public async Task Pay_ReturnsFailureResponse()
        {
            var result = await _service.Pay(
                [new ResolvedCartLine(Guid.NewGuid(), null, 1, 10m, "Product", null, null, null, null, null)]);

            Assert.False(result.Success);
            Assert.Equal("PayPal payments are not currently available.", result.Message);
        }

        [Fact]
        public async Task CaptureAsync_ReturnsFalse()
        {
            var result = await _service.CaptureAsync("demo-token");

            Assert.False(result);
        }
    }
}
