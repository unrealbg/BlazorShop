namespace BlazorShop.Infrastructure.Services
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Entities;

    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Stripe.Checkout;

    public class StripePaymentService : IPaymentService
    {
        private readonly IStripeCheckoutSessionService _checkoutSessionService;
        private readonly ClientAppOptions _clientAppOptions;
        private readonly ILogger<StripePaymentService> _logger;

        public StripePaymentService(
            IStripeCheckoutSessionService checkoutSessionService,
            IOptions<ClientAppOptions> clientAppOptions,
            ILogger<StripePaymentService> logger)
        {
            _checkoutSessionService = checkoutSessionService;
            _clientAppOptions = clientAppOptions.Value;
            _logger = logger;
        }

        public async Task<ServiceResponse> Pay(decimal totalAmount, IEnumerable<Product> cartProducts, IEnumerable<ProcessCart> carts)
        {
            try
            {
                var lineItems = new List<SessionLineItemOptions>();

                foreach (var item in cartProducts)
                {
                    var pQuantity = carts.FirstOrDefault(_ => _.ProductId == item.Id);

                    lineItems.Add(new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "eur",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = item.Name,
                                Description = item.Description
                            },
                            UnitAmount = (long)(item.Price * 100),
                        },

                        Quantity = pQuantity!.Quantity,
                    });
                }

                var opt = new SessionCreateOptions
                {
                    PaymentMethodTypes = ["card"],
                    LineItems = lineItems,
                    Mode = "payment",
                    SuccessUrl = this.BuildClientUrl("payment-success?pm=card"),
                    CancelUrl = this.BuildClientUrl("payment-cancel"),
                };

                var session = await _checkoutSessionService.CreateAsync(opt);

                return new ServiceResponse(true, session.Url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Stripe checkout session.");
                return new ServiceResponse(false, "Unable to initialize the card payment session. Please try again later.");
            }
        }

        private string BuildClientUrl(string path)
        {
            return $"{_clientAppOptions.BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
        }
    }
}
