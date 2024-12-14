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

        public PaymentController(IPaymentMethodService paymentMethodService)
        {
            _paymentMethodService = paymentMethodService;
        }

        /// <summary>
        /// Get all payment methods
        /// </summary>
        /// <returns>The payment methods </returns>
        [HttpGet("payment-methods")]
        public async Task<ActionResult<IEnumerable<GetPaymentMethod>>> GetPaymentMethods()
        {
            var paymentMethods = await _paymentMethodService.GetPaymentMethodsAsync();
            return !paymentMethods.Any() ? this.NotFound() : this.Ok(paymentMethods);
        }
    }
}
