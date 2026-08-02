namespace BlazorShop.Application.Services.Payment
{
    using AutoMapper;

    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Contracts.Payment;

    using Microsoft.Extensions.Options;

    public class PaymentMethodService : IPaymentMethodService
    {
        private static readonly string[] DisabledPaymentMethodNames = ["PayPal"];

        private readonly IPaymentMethod _paymentMethod;
        private readonly IMapper _mapper;
        private readonly StripeOptions _stripeOptions;

        public PaymentMethodService(
            IPaymentMethod paymentMethod,
            IMapper mapper,
            IOptions<StripeOptions> stripeOptions)
        {
            this._paymentMethod = paymentMethod;
            this._mapper = mapper;
            this._stripeOptions = stripeOptions.Value;
        }

        public async Task<IEnumerable<GetPaymentMethod>> GetPaymentMethodsAsync()
        {
            var methods = await this._paymentMethod.GetPaymentMethodsAsync();

            if (methods == null || !methods.Any())
            {
                return [];
            }

            var supportedMethods = methods
                .Where(method => !DisabledPaymentMethodNames.Contains(method.Name, StringComparer.OrdinalIgnoreCase))
                .Where(method => this._stripeOptions.Enabled
                    || !string.Equals(method.Name, "Credit Card", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (supportedMethods.Count == 0)
            {
                return [];
            }

            return this._mapper.Map<IEnumerable<GetPaymentMethod>>(supportedMethods);
        }
    }
}
