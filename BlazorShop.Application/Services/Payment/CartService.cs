namespace BlazorShop.Application.Services.Payment
{
    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Payment;

    public class CartService : ICartService
    {
        private readonly ICart _cart;
        private readonly IMapper _mapper;
        private readonly IGenericRepository<Product> _productRepository;
        private readonly IPaymentMethodService _paymentMethodService;
        private readonly IPaymentService _paymentService;

        public CartService(ICart cart,
                           IMapper mapper,
                           IGenericRepository<Product> productRepository,
                           IPaymentMethodService paymentMethodService,
                           IPaymentService paymentService)
        {
            this._cart = cart;
            this._mapper = mapper;
            this._productRepository = productRepository;
            this._paymentMethodService = paymentMethodService;
            this._paymentService = paymentService;
        }

        public async Task<ServiceResponse> SaveCheckoutHistoryAsync(IEnumerable<CreateAchieve> achieves)
        {
            var mappedData = this._mapper.Map<IEnumerable<Achieve>>(achieves);
            var result = await this._cart.SaveCheckoutHistory(mappedData);

            return result > 0 ? new ServiceResponse(true, "Checkout history saved successfully") : new ServiceResponse(false, "Failed to save checkout history");
        }

        public async Task<ServiceResponse> CheckoutAsync(Checkout checkout)
        {
            var (products, totalAmount) = await this.GetCartTotalAmount(checkout.Carts);
            var paymentMethods = await this._paymentMethodService.GetPaymentMethodsAsync();

            if (checkout.PaymentMethodId == paymentMethods.FirstOrDefault()!.Id)
            {
                return await this._paymentService.Pay(totalAmount, products, checkout.Carts);
            }

            return new ServiceResponse(false, "Invalid payment method");
        }

        private async Task<(IEnumerable<Product>, decimal)> GetCartTotalAmount(IEnumerable<ProcessCart> carts)
        {
            if (!carts.Any())
            {
                return ([], 0);
            }

            var products = await this._productRepository.GetAllAsync();

            if (!products.Any())
            {
                return ([], 0);
            }

            var cartProducts = carts
                .Select(ci => products
                    .FirstOrDefault(p => p.Id == ci.ProductId))
                .Where(p => p != null)
                .ToList();

            var totalAmount = carts
                .Where(ci => cartProducts.Any(p => p.Id == ci.ProductId))
                .Sum(ci => ci.Quantity * cartProducts
                    .First(p => p.Id == ci.ProductId)!
                    .Price);

            return (cartProducts!, totalAmount);
        }
    }
}
