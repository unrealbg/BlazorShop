namespace BlazorShop.Application.Services.Payment
{
    using AutoMapper;

    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Contracts.Payment;

    public class PaymentMethodService : IPaymentMethodService
    {
        private static readonly string[] DisabledPaymentMethodNames = ["PayPal"];

        private readonly IPaymentMethod _paymentMethod;
        private readonly IMapper _mapper;

        public PaymentMethodService(IPaymentMethod paymentMethod, IMapper mapper)
        {
            this._paymentMethod = paymentMethod;
            this._mapper = mapper;
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
                .ToList();

            if (supportedMethods.Count == 0)
            {
                return [];
            }

            return this._mapper.Map<IEnumerable<GetPaymentMethod>>(supportedMethods);
        }
    }
}
