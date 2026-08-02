namespace BlazorShop.API.Controllers
{
    using System.IO;

    using BlazorShop.Infrastructure.Services;

    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.RateLimiting;

    [ApiController]
    [Route("api/stripe/webhook")]
    [AllowAnonymous]
    [DisableRateLimiting]
    public sealed class StripeWebhookController : ControllerBase
    {
        private readonly IStripeWebhookService _webhookService;

        public StripeWebhookController(IStripeWebhookService webhookService)
        {
            _webhookService = webhookService;
        }

        [HttpPost]
        [Consumes("application/json")]
        public async Task<IActionResult> Handle(CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync(cancellationToken);
            var signature = Request.Headers["Stripe-Signature"].ToString();
            var result = await _webhookService.HandleAsync(payload, signature, cancellationToken);

            return result switch
            {
                StripeWebhookHandlingResult.Processed => Ok(),
                StripeWebhookHandlingResult.Ignored => Ok(),
                StripeWebhookHandlingResult.OrderNotFound => NotFound(),
                _ => BadRequest(),
            };
        }
    }
}
