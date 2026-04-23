namespace BlazorShop.Web.Pages.Payments
{
    using System.Net;
    using System.Text.Json;

    using BlazorShop.Web.Services;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models.Notifications;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;
    using Microsoft.AspNetCore.Components;

    public partial class Cart
    {
        private List<ProcessCart> _myCarts = [];
        private IReadOnlyList<CheckoutCartLine> _cartLines = [];
        private IEnumerable<GetProduct> _products = [];
        private IEnumerable<GetPaymentMethod> _paymentMethods = [];
        private bool _isCartLoading;
        private string? _cartLoadError;
        private bool _isPaymentMethodsLoading;
        private string? _paymentMethodsError;
        private bool _isSavingCart;
        private string? _busyCartLineKey;
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
            await LoadCartAsync();
        }

        private async Task LoadCartAsync()
        {
            _isCartLoading = true;
            _cartLoadError = null;
            _paymentMethodsError = null;
            _showPaymentDialog = false;

            _myCarts = [];
            var cartJson = await this.CookieStorageService.GetAsync(Constant.Cart.Name);
            if (!string.IsNullOrEmpty(cartJson))
            {
                _myCarts = JsonSerializer.Deserialize<List<ProcessCart>>(cartJson) ?? [];
            }

            if (!await this.LoadCartProductsAsync())
            {
                _cartLines = [];
                _paymentMethods = [];
                _isCartLoading = false;
                return;
            }

            this.BuildCartLines();
            await LoadPaymentMethodsAsync();
            _isCartLoading = false;
        }

        private async Task LoadPaymentMethodsAsync()
        {
            _isPaymentMethodsLoading = true;
            _paymentMethodsError = null;

            var paymentMethodsResult = await this.PaymentMethodService.GetPaymentMethods();
            if (this.QueryFailureNotifier.TryNotifyFailure(paymentMethodsResult, "Checkout", showToast: false))
            {
                _paymentMethods = [];
                _paymentMethodsError = FeedbackMessageResolver.ResolveQueryFailure(paymentMethodsResult, "We couldn't load payment methods right now. Please try again.");
                _isPaymentMethodsLoading = false;
                return;
            }

            _paymentMethods = paymentMethodsResult.Data ?? [];
            _isPaymentMethodsLoading = false;
        }

        private bool HasUnavailableCartLines => _cartLines.Any(line => line.IsUnavailable);

        private decimal CartGrandTotal => _cartLines.Sum(line => line.LineTotal);

        private void BuildCartLines()
        {
            _cartLines = CheckoutCartLineMapper.Build(_myCarts, _products);
        }

        private async Task<bool> LoadCartProductsAsync()
        {
            var uniqueProductIds = _myCarts
                .Select(cart => cart.ProductId)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToArray();

            if (uniqueProductIds.Length == 0)
            {
                _products = [];
                return true;
            }

            var productTasks = uniqueProductIds.Select(id => this.ProductService.GetByIdAsync(id));
            var productResults = await Task.WhenAll(productTasks);
            var successfulProducts = new List<GetProduct>();

            foreach (var productResult in productResults)
            {
                if (productResult.Success && productResult.Data is not null)
                {
                    successfulProducts.Add(productResult.Data);
                    continue;
                }

                if (productResult.StatusCode == HttpStatusCode.NotFound)
                {
                    continue;
                }

                _products = [];
                _cartLoadError = FeedbackMessageResolver.ResolveQueryFailure(productResult, "We couldn't refresh your cart right now. Please try again.");
                return false;
            }

            _products = successfulProducts;
            return true;
        }

        private ProcessCart? GetCartItem(Guid productId, Guid? variantId)
        {
            return _myCarts.FirstOrDefault(x => x.ProductId == productId && x.VariantId == variantId);
        }

        private static string GetCartLineKey(Guid productId, Guid? variantId)
        {
            return $"{productId}:{variantId?.ToString() ?? "none"}";
        }

        private bool IsCartLineBusy(Guid productId, Guid? variantId)
        {
            return _busyCartLineKey == GetCartLineKey(productId, variantId) || _processingMethodId.HasValue || _isSavingCart;
        }

        private async Task HandleInputChange(ChangeEventArgs e, Guid productId, Guid? variantId)
        {
            var cartItem = GetCartItem(productId, variantId);
            if (cartItem is null || IsCartLineBusy(productId, variantId))
            {
                return;
            }

            try
            {
                var newQuantity = int.Parse(e.Value?.ToString() ?? "0");
                if (newQuantity == cartItem.Quantity)
                {
                    return;
                }

                _isSavingCart = true;
                _busyCartLineKey = GetCartLineKey(productId, variantId);

                if (newQuantity <= 0)
                {
                    _myCarts.RemoveAll(x => x.ProductId == productId && x.VariantId == variantId);
                    await this.SaveCart(_myCarts);
                    this.BuildCartLines();
                    this.NotificationService.NotifySuccess("Product removed from cart.", "Cart", NotificationKind.Order, addToInbox: false);
                    return;
                }

                cartItem.Quantity = newQuantity;
                await this.SaveCart(_myCarts);
                this.BuildCartLines();
                this.NotificationService.NotifySuccess("Cart quantity updated.", "Cart", NotificationKind.Order, addToInbox: false);
            }
            catch
            {
                this.NotificationService.NotifyError("Invalid quantity", "Cart", NotificationKind.Order, addToInbox: false);
            }
            finally
            {
                _isSavingCart = false;
                _busyCartLineKey = null;
                this.StateHasChanged();
            }
        }

        private async Task SaveCart(List<ProcessCart> myCarts)
        {
            _myCarts = myCarts;
            await this.CookieStorageService.SetAsync(Constant.Cart.Name, JsonSerializer.Serialize(_myCarts), 30);
        }

        private async Task RemoveCartItem(Guid productId, Guid? variantId)
        {
            if (IsCartLineBusy(productId, variantId))
            {
                return;
            }

            _isSavingCart = true;
            _busyCartLineKey = GetCartLineKey(productId, variantId);
            _myCarts.RemoveAll(x => x.ProductId == productId && x.VariantId == variantId);

            try
            {
                this.NotificationService.NotifySuccess("Product removed from cart.", "Cart", NotificationKind.Order, addToInbox: false);

                await this.CookieStorageService.RemoveAsync(Constant.Cart.Name);
                await this.SaveCart(_myCarts);
                this.BuildCartLines();
                this.StateHasChanged();
            }
            finally
            {
                _isSavingCart = false;
                _busyCartLineKey = null;
            }
        }

        private static bool TryGetProp(JsonElement obj, string name, out JsonElement value)
        {
            if (obj.TryGetProperty(name, out value)) return true;
            // Fallback to PascalCase if API happens to send it
            var pascal = char.ToUpperInvariant(name[0]) + name.Substring(1);
            return obj.TryGetProperty(pascal, out value);
        }

        private async Task SelectPaymentMethod(GetPaymentMethod paymentMethod)
        {
            if (paymentMethod is null)
            {
                return;
            }

            if (HasUnavailableCartLines)
            {
                this.NotificationService.NotifyWarning(
                    "Remove unavailable items from the cart before checkout continues.",
                    "Checkout",
                    NotificationKind.Order,
                    addToInbox: false);
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
                                if (payload.ValueKind == JsonValueKind.Object)
                                {
                                    _btInfo = new BtInfo
                                    {
                                        Iban = TryGetProp(payload, "iban", out var iban) ? (iban.GetString() ?? string.Empty) : string.Empty,
                                        Beneficiary = TryGetProp(payload, "beneficiary", out var ben) ? (ben.GetString() ?? string.Empty) : string.Empty,
                                        BankName = TryGetProp(payload, "bankName", out var bank) ? (bank.GetString() ?? string.Empty) : string.Empty,
                                        Reference = TryGetProp(payload, "reference", out var @ref) ? (@ref.GetString() ?? string.Empty) : string.Empty,
                                        Amount = TryGetProp(payload, "amount", out var amt) ? (amt.TryGetDecimal(out var d) ? d : 0m) : 0m,
                                        AdditionalInfo = TryGetProp(payload, "additionalInfo", out var ai) ? ai.GetString() : null
                                    };
                                }
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
                            var successPath = string.Equals(paymentMethod.Name, "Cash on Delivery", StringComparison.OrdinalIgnoreCase)
                                ? "/payment-success?pm=cod"
                                : "/payment-success?pm=card";
                            this.NavigationManager.NavigateTo(successPath, true);
                        }
                    }
                }
                else
                {
                    this.NotificationService.NotifyError(result.Message ?? "Payment processing failed.", "Checkout", NotificationKind.Payment);
                }
            }
            catch
            {
                this.NotificationService.NotifyError("Payment processing failed.", "Checkout", NotificationKind.Payment);
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
            this.NavigationManager.NavigateTo("/authentication/login/account");
        }

        private Task RetryLoadCartAsync()
        {
            return LoadCartAsync();
        }

        private Task RetryLoadPaymentMethodsAsync()
        {
            return LoadPaymentMethodsAsync();
        }

        private void Cancel()
        {
            _showPaymentDialog = false;
            _processingMethodId = null;
        }
    }
}