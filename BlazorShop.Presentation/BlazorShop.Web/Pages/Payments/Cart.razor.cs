namespace BlazorShop.Web.Pages.Payments
{
    using System.Text.Json;

    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Toast;
    using Microsoft.AspNetCore.Components;

    public partial class Cart
    {
        private List<ProcessCart> _myCarts = [];
        private List<GetProduct> _selectedProducts = [];
        private IEnumerable<GetProduct> _products = [];
        private IEnumerable<GetPaymentMethod> _paymentMethods = [];
        private Guid? _processingMethodId = null;
        private bool _showPaymentDialog;

        private bool _showBtDialog;
        private BtInfo _btInfo = new();

        private sealed class BtInfo
        {
            public string Iban { get; set; } = string.Empty;
            public string Beneficiary { get; set; } = string.Empty;
            public string BankName { get; set; } = string.Empty;
            public string Reference { get; set; } = string.Empty;
            public decimal Amount { get; set; }
            public string? AdditionalInfo { get; set; }
        }

        protected override async Task OnInitializedAsync()
        {
            try
            {
                var cartJson = await this.CookieStorageService.GetAsync(Constant.Cart.Name);
                if (!string.IsNullOrEmpty(cartJson))
                {
                    _myCarts = JsonSerializer.Deserialize<List<ProcessCart>>(cartJson) ?? [];
                }

                _products = await this.ProductService.GetAllAsync();
                this.GetCart();

                _paymentMethods = await this.PaymentMethodService.GetPaymentMethods();
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
            return _myCarts.Where(x => x.ProductId == productId).Sum(x => x.Quantity);
        }

        private ProcessCart? GetCartItem(Guid productId)
        {
            return _myCarts.FirstOrDefault(x => x.ProductId == productId && x.VariantId == null);
        }

        private decimal GetProductTotalPrice(Guid productId)
        {
            var lineItems = _myCarts.Where(x => x.ProductId == productId);
            decimal total = 0m;
            foreach (var li in lineItems)
            {
                var price = li.UnitPrice ?? _products.FirstOrDefault(x => x.Id == li.ProductId)?.Price ?? 0m;
                total += li.Quantity * price;
            }
            return total;
        }

        private async Task HandleInputChange(ChangeEventArgs e, Guid productId)
        {
            try
            {
                var newQuantity = int.Parse(e.Value?.ToString() ?? "0");
                var item = _myCarts.FirstOrDefault(x => x.ProductId == productId && x.VariantId == null);
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
            decimal total = 0m;
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
            if (paymentMethod is null)
            {
                return;
            }

            _processingMethodId = paymentMethod.Id;
            StateHasChanged();

            try
            {
                var checkout = new Checkout() { PaymentMethodId = paymentMethod.Id, Carts = _myCarts };
                var result = await this.CartService.Checkout(checkout);

                if (result.Success)
                {
                    var paymentLink = result.Message;
                    if (!string.IsNullOrWhiteSpace(paymentLink) && (paymentLink.StartsWith("http://") || paymentLink.StartsWith("https://")))
                    {
                        this.NavigationManager.NavigateTo(paymentLink, true);
                    }
                    else
                    {
                        var isBankTransfer = string.Equals(paymentMethod.Name, "Bank Transfer", StringComparison.OrdinalIgnoreCase);
                        if (isBankTransfer && result.Payload.HasValue)
                        {
                            try
                            {
                                var payload = result.Payload.Value;
                                _btInfo = new BtInfo
                                {
                                    Iban = payload.GetProperty("Iban").GetString() ?? string.Empty,
                                    Beneficiary = payload.GetProperty("Beneficiary").GetString() ?? string.Empty,
                                    BankName = payload.GetProperty("BankName").GetString() ?? string.Empty,
                                    Reference = payload.GetProperty("Reference").GetString() ?? string.Empty,
                                    Amount = payload.GetProperty("Amount").GetDecimal(),
                                    AdditionalInfo = payload.TryGetProperty("AdditionalInfo", out var ai) ? ai.GetString() : null
                                };
                            }
                            catch { }

                            _showPaymentDialog = false;
                            _showBtDialog = true;
                            StateHasChanged();
                        }
                        else
                        {
                            _showPaymentDialog = false;
                            this.StateHasChanged();
                            this.NavigationManager.NavigateTo("/payment-success", true);
                        }
                    }
                }
                else
                {
                    this.ToastService.ShowToast(ToastLevel.Error, result.Message ?? "Payment processing failed.", "Checkout", ToastIcon.Error);
                }
            }
            catch
            {
                this.ToastService.ShowToast(ToastLevel.Error, "Payment processing failed.", "Checkout", ToastIcon.Error);
            }
            finally
            {
                _processingMethodId = null;
                StateHasChanged();
            }
        }

        private void ConfirmBankTransfer()
        {
            _showBtDialog = false;
            this.NavigationManager.NavigateTo("/payment-success?bt=1", true);
        }

        private void Checkout()
        {
            this.NavigationManager.NavigateTo($"authentication/login/{Constant.Cart.Name}");
        }

        private void Cancel()
        {
            _showPaymentDialog = false;
            _processingMethodId = null;
        }
    }
}