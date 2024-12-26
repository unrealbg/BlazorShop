namespace BlazorShop.Application.Services.Payment
{
    using AutoMapper;

    using BlazorShop.Application.DTOs;
    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Authentication;
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
        private readonly IAppUserManager _userManager;

        public CartService(ICart cart,
                           IMapper mapper,
                           IGenericRepository<Product> productRepository,
                           IPaymentMethodService paymentMethodService,
                           IPaymentService paymentService,
                           IAppUserManager userManager)
        {
            _cart = cart;
            _mapper = mapper;
            _productRepository = productRepository;
            _paymentMethodService = paymentMethodService;
            _paymentService = paymentService;
            _userManager = userManager;
        }

        public async Task<ServiceResponse> SaveCheckoutHistoryAsync(IEnumerable<CreateOrderItem> orderItems)
        {
            var mappedData = _mapper.Map<IEnumerable<OrderItem>>(orderItems);
            var result = await _cart.SaveCheckoutHistory(mappedData);

            return result > 0 ? new ServiceResponse(true, "Checkout history saved successfully") : new ServiceResponse(false, "Failed to save checkout history");
        }

        public async Task<ServiceResponse> CheckoutAsync(Checkout checkout)
        {
            var (products, totalAmount) = await this.GetCartTotalAmount(checkout.Carts);
            var paymentMethods = await _paymentMethodService.GetPaymentMethodsAsync();

            if (checkout.PaymentMethodId == paymentMethods.FirstOrDefault()!.Id)
            {
                return await _paymentService.Pay(totalAmount, products, checkout.Carts);
            }

            return new ServiceResponse(false, "Invalid payment method");
        }

        public async Task<IEnumerable<GetOrderItem>> GetOrderItemsAsync()
        {
            var history = await _cart.GetAllCheckoutHistory();

            if (history == null)
            {
                return [];
            }

            var groupByCustomerId = history.GroupBy(x => x.UserId).ToList();
            var products = await _productRepository.GetAllAsync();
            var orderItems = new List<GetOrderItem>();

            foreach (var customerId in groupByCustomerId)
            {
                var customerDetails = await _userManager.GetUserByIdAsync(customerId.Key!);

                foreach (var item in customerId)
                {
                    var product = products.FirstOrDefault(p => p.Id == item.ProductId);

                    orderItems.Add(new GetOrderItem
                    {
                        CustomerName = customerDetails?.UserName,
                        CustomerEmail = customerDetails?.Email,
                        ProductName = product?.Name,
                        AmountPayed = item.Quantity * product!.Price,
                        QuantityOrdered = item.Quantity,
                        DatePurchased = item.CreatedOn
                    });
                }
            }

            return orderItems;
        }

        private async Task<(IEnumerable<Product>, decimal)> GetCartTotalAmount(IEnumerable<ProcessCart> carts)
        {
            if (!carts.Any())
            {
                return ([], 0);
            }

            var products = await _productRepository.GetAllAsync();

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

        public async Task<IEnumerable<GetOrderItem>> GetCheckoutHistoryByUserId(string userId)
        {
            // Вземете историята на поръчките само за дадения потребител
            var history = await _cart.GetCheckoutHistoryByUserId(userId);

            if (history == null || !history.Any())
            {
                return new List<GetOrderItem>();
            }

            // Вземете всички продукти
            var products = await _productRepository.GetAllAsync();

            // Създайте списък с поръчки за потребителя
            var orderItems = new List<GetOrderItem>();

            // Вземете детайлите на потребителя
            var customerDetails = await _userManager.GetUserByIdAsync(userId);

            foreach (var item in history)
            {
                var product = products.FirstOrDefault(p => p.Id == item.ProductId);

                orderItems.Add(new GetOrderItem
                                   {
                                       CustomerName = customerDetails?.UserName,
                                       CustomerEmail = customerDetails?.Email,
                                       ProductName = product?.Name,
                                       AmountPayed = item.Quantity * (product?.Price ?? 0),
                                       QuantityOrdered = item.Quantity,
                                       DatePurchased = item.CreatedOn
                                   });
            }

            return orderItems;
        }
    }
}
