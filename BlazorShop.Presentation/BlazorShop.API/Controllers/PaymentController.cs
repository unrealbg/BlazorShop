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
            IOptions<ClientAppOptions> clientAppOptions)
        {
            _paymentMethodService = paymentMethodService;
            _payPalPaymentService = payPalPaymentService;
            _clientAppOptions = clientAppOptions.Value;
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
        /// PayPal capture redirect destination.
        /// </summary>
        [HttpGet("paypal/capture")]
        public async Task<IActionResult> CapturePayPal([FromQuery] string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return BadRequest("Missing token");
            var ok = await _payPalPaymentService.CaptureAsync(token);
            if (!ok) return Redirect(this.BuildClientUrl("payment-cancel"));

            return Redirect(this.BuildClientUrl("payment-success"));
        }

        private string BuildClientUrl(string path)
        {
            return $"{_clientAppOptions.BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
        }
    }
}
