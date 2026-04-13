namespace BlazorShop.Infrastructure.Services
{
    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Entities;

    using Microsoft.Extensions.Options;

    using Stripe.Checkout;

    public class StripePaymentService : IPaymentService
    {
        private readonly ClientAppOptions _clientAppOptions;

        public StripePaymentService(IOptions<ClientAppOptions> clientAppOptions)
        {
            _clientAppOptions = clientAppOptions.Value;
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
                    SuccessUrl = this.BuildClientUrl("payment-success"),
                    CancelUrl = this.BuildClientUrl("payment-cancel"),
                };

                var service = new SessionService();
                var session = await service.CreateAsync(opt);

                return new ServiceResponse(true, session.Url);
            }
            catch (Exception ex)
            {
                return new ServiceResponse(false, ex.Message);
            }
        }

        private string BuildClientUrl(string path)
        {
            return $"{_clientAppOptions.BaseUrl.TrimEnd('/')}/{path.TrimStart('/')}";
        }
    }
}
