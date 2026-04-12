namespace BlazorShop.API.Controllers
{
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts.Payment;

    using Microsoft.Extensions.Options;

    using Microsoft.AspNetCore.Mvc;

    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentMethodService _paymentMethodService;
        private readonly IPayPalPaymentService _payPalPaymentService;
        private readonly ClientAppOptions _clientAppOptions;

        public PaymentController(
            IPaymentMethodService paymentMethodService,
            IPayPalPaymentService payPalPaymentService,
            IOptions<ClientAppOptions>? clientAppOptions = null)
        {
            _paymentMethodService = paymentMethodService;
            _payPalPaymentService = payPalPaymentService;
            _clientAppOptions = clientAppOptions?.Value ?? new ClientAppOptions();
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

            return Redirect(this.BuildClientUrl("payment-success"));
        }

        private string BuildClientUrl(string path)
        {
            var baseUrl = _clientAppOptions.BaseUrl;

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = "https://localhost:7258";
            }

            return $"{baseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
        }
    }
}
