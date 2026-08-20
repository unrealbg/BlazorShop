namespace BlazorShop.Infrastructure.Services
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts.Payment;
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

        public async Task<ServiceResponse> Pay(IReadOnlyCollection<ResolvedCartLine> lines, Guid orderId)
        {
            try
            {
                var lineItems = new List<SessionLineItemOptions>();

                foreach (var line in lines)
                {
                    lineItems.Add(new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            Currency = "eur",
                            ProductData = new SessionLineItemPriceDataProductDataOptions
                            {
                                Name = line.ProductName,
                                Description = BuildDescription(line),
                            },
                            UnitAmount = (long)decimal.Round(line.UnitPrice * 100, 0, MidpointRounding.AwayFromZero),
                        },

                        Quantity = line.Quantity,
                    });
                }

                var opt = new SessionCreateOptions
                {
                    PaymentMethodTypes = ["card"],
                    LineItems = lineItems,
                    Mode = "payment",
                    ClientReferenceId = orderId.ToString("D"),
                    Metadata = new Dictionary<string, string>
                    {
                        ["order_id"] = orderId.ToString("D"),
                    },
                    PaymentIntentData = new SessionPaymentIntentDataOptions
                    {
                        Metadata = new Dictionary<string, string>
                        {
                            ["order_id"] = orderId.ToString("D"),
                        },
                    },
                    SuccessUrl = this.BuildClientUrl("payment-success?pm=card&session_id={CHECKOUT_SESSION_ID}"),
                    CancelUrl = this.BuildClientUrl($"payment-cancel?order_id={orderId:D}"),
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

        private static string? BuildDescription(ResolvedCartLine line)
        {
            var variantDetails = new[]
                {
                    string.IsNullOrWhiteSpace(line.Sku) ? null : $"SKU: {line.Sku}",
                    string.IsNullOrWhiteSpace(line.SizeValue) ? null : $"Size: {line.SizeValue}",
                    string.IsNullOrWhiteSpace(line.Color) ? null : $"Color: {line.Color}",
                }
                .Where(value => value is not null)
                .ToArray();

            return variantDetails.Length > 0
                ? string.Join(", ", variantDetails)
                : line.ProductDescription;
        }
    }
}
