namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;

    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentMethodService _paymentMethodService;
        private readonly IPayPalPaymentService _payPalPaymentService;

        public PaymentController(IPaymentMethodService paymentMethodService, IPayPalPaymentService payPalPaymentService)
        {
            _paymentMethodService = paymentMethodService;
            _payPalPaymentService = payPalPaymentService;
        }

        /// <summary>
        /// Get all payment methods
        /// </summary>
        /// <returns>The payment methods </returns>
        [HttpGet("methods")]
        public async Task<ActionResult<IEnumerable<GetPaymentMethod>>> GetPaymentMethods()
        {
            var paymentMethods = await _paymentMethodService.GetPaymentMethodsAsync();
            return !paymentMethods.Any() ? this.NotFound() : this.Ok(paymentMethods);
        }

        /// <summary>
        /// PayPal capture redirect destination (stub implementation)
        /// </summary>
        [HttpGet("paypal/capture")]
        public async Task<IActionResult> CapturePayPal([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return BadRequest("Missing token");
            var ok = await _payPalPaymentService.CaptureAsync(token);
            if (!ok) return BadRequest("Capture failed");

            return Redirect("https://localhost:7258/payment-success");
        }
    }
}
