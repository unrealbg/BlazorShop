namespace BlazorShop.Web.Pages.Payments
{
    using System.Text.Json;

    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Toast;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Authorization;

    public partial class Cart
    {
        private string? _paying;

        private bool _showPaymentDialog = false;

        private IEnumerable<GetPaymentMethod> _paymentMethods = [];

        private List<GetProduct> _selectedProducts = [];

        private IEnumerable<GetProduct> _products = [];

        private List<ProcessCart> _myCarts = [];

        [CascadingParameter]
        public Task<AuthenticationState>? UserAuthState { get; set; }

        protected override async Task OnInitializedAsync()
        {
            try
            {
                _paymentMethods = await this.PaymentMethodService.GetPaymentMethods();
                _products = await this.ProductService.GetAllAsync();
            }
            catch
            {
                this.ToastService.ShowToast(
                    ToastLevel.Error,
                    "An error occurred while loading the page",
                    "Cart",
                    ToastIcon.Error);
            }

            var cartString = await this.CookieStorageService.GetAsync(Constant.Cart.Name);

            if (string.IsNullOrEmpty(cartString))
            {
                return;
            }

            var carts = JsonSerializer.Deserialize<List<ProcessCart>>(cartString);
            _myCarts = carts ?? [];

            if (!_myCarts.Any())
            {
                return;
            }

            this.GetCart();
        }

        private async Task Checkout()
        {
            try
            {
                var user = (await this.UserAuthState!)!.User;
                if (user?.Identity?.IsAuthenticated != true)
                {
                    this.NavigationManager.NavigateTo($"authentication/login/{Constant.Cart.Name}");
                }
                else
                {
                    _showPaymentDialog = true;
                }
            }
            catch
            {
                this.NavigationManager.NavigateTo($"authentication/login/{Constant.Cart.Name}");
            }
        }

        private void GetCart()
        {
            _selectedProducts.Clear();

            foreach (var processCart in _myCarts)
            {
                var product = _products.FirstOrDefault(x => x.Id == processCart.ProductId);

                if (product is not null && !_selectedProducts.Contains(product))
                {
                    _selectedProducts.Add(product);
                }
            }

            _selectedProducts = _selectedProducts.OrderBy(x => x.Name).ToList();
        }

        private int GetProductQuantity(Guid productId)
        {
            return this.GetCartItem(productId)?.Quantity ?? 0;
        }

        private ProcessCart? GetCartItem(Guid productId)
        {
            return _myCarts.FirstOrDefault(x => x.ProductId == productId);
        }

        private decimal GetProductTotalPrice(Guid productId)
        {
            var quantity = this.GetProductQuantity(productId);
            var price = _products.FirstOrDefault(x => x.Id == productId)?.Price ?? 0m;
            return quantity * price;
        }

        private async Task HandleInputChange(ChangeEventArgs e, Guid productId)
        {
            try
            {
                var newQuantity = int.Parse(e.Value?.ToString() ?? "0");
                var item = _myCarts.FirstOrDefault(x => x.ProductId == productId);
                if (item is not null)
                {
                    item.Quantity = newQuantity;
                    await this.SaveCart(_myCarts);
                    this.GetCart();
                }
            }
            catch
            {
                this.ToastService.ShowToast(ToastLevel.Error, "Invalid quantity", "Cart", ToastIcon.Error);
            }
            finally
            {
                this.StateHasChanged();
            }
        }

        private async Task SaveCart(List<ProcessCart> myCarts)
        {
            _myCarts = myCarts;
            await this.CookieStorageService.SetAsync(Constant.Cart.Name, JsonSerializer.Serialize(_myCarts), 30);
        }

        private decimal GetGrandTotal(List<GetProduct> products)
        {
            var total = 0.00m;

            foreach (var product in products)
            {
                total += this.GetProductTotalPrice(product.Id);
            }

            return total;
        }

        private async Task RemoveCartItem(Guid productId)
        {
            var cartItem = this.GetCartItem(productId);
            if (cartItem is not null)
            {
                _myCarts.Remove(cartItem);
            }

            var product = _selectedProducts.FirstOrDefault(x => x.Id == productId);
            if (product is not null)
            {
                _selectedProducts.Remove(product);
            }

            this.ToastService.ShowToast(ToastLevel.Warning, "Product removed from cart", "Cart", ToastIcon.Warning);

            await this.CookieStorageService.RemoveAsync(Constant.Cookie.Name);
            await this.SaveCart(_myCarts);
        }

        private async Task SelectPaymentMethod(GetPaymentMethod paymentMethod)
        {
            if (paymentMethod is not null)
            {
                _paying = "Processing... please wait";

                try
                {
                    var checkout = new Checkout() { PaymentMethodId = paymentMethod.Id, Carts = _myCarts };

                    var (success, paymentLink) = await this.CartService.Checkout(checkout);

                    if (success)
                    {
                        _paying = null;
                        this.NavigationManager.NavigateTo(paymentLink);
                    }
                    else
                    {
                        this.ToastService.ShowToast(ToastLevel.Error, "Checkout failed. Please try again.", "Checkout");
                    }
                }
                finally
                {
                    _paying = null;
                    _showPaymentDialog = false;
                }
            }
        }

        private void Cancel()
        {
            _paying = null;
            _showPaymentDialog = false;
        }
    }
}