namespace BlazorShop.Web.Pages.Products
{
    using System.Text.Json;

    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Toast;

    using Microsoft.AspNetCore.Components;

    public partial class NewReleases
    {
        private IEnumerable<GetProduct> _newReleases = [];

        private bool _isAddingToCart = false;

        private List<ProcessCart> _myCarts = [];

        private bool _showModal = false;

        [Parameter]
        public GetProduct SelectedProduct { get; set; } = new();

        protected override async Task OnInitializedAsync()
        {
            var cartJson = await this.CookieStorageService.GetAsync(Constant.Cart.Name);
            if (!string.IsNullOrEmpty(cartJson))
            {
                _myCarts = JsonSerializer.Deserialize<List<ProcessCart>>(cartJson) ?? [];
            }

            var allProductsResult = await this.ProductService.GetAllAsync();
            if (this.QueryFailureNotifier.TryNotifyFailure(allProductsResult, "Products"))
            {
                _newReleases = [];
                return;
            }

            var allProducts = allProductsResult.Data ?? [];
            _newReleases = allProducts.Where(p => p.IsNew).ToList();
        }

        private async Task HandleAddToCart(GetProduct product)
        {
            if (product.Variants?.Any() == true)
            {
                ShowDetails(product);
                return;
            }

            await AddItemToCart(product.Id);
        }

        private async Task AddItemToCart(Guid productId)
        {
            if (_isAddingToCart) return;

            try
            {
                _isAddingToCart = true;

                var getCart = _myCarts.FirstOrDefault(x => x.ProductId == productId && x.VariantId == null);
                var productResult = await this.ProductService.GetByIdAsync(productId);
                if (this.QueryFailureNotifier.TryNotifyFailure(productResult, "Cart", ToastPosition.BottomRight) ||
                    productResult.Data is null)
                {
                    return;
                }

                var product = productResult.Data;
                var productName = product.Name;

                if (getCart == null)
                {
                    _myCarts.Add(new ProcessCart { ProductId = productId, Quantity = 1, UnitPrice = product.Price });

                    this.ToastService.ShowToast(
                        ToastLevel.Success,
                        $"Product {productName} added to cart",
                        "Cart",
                        ToastIcon.Success,
                        ToastPosition.BottomRight);
                }
                else
                {
                    getCart.Quantity++;
                    this.ToastService.ShowToast(
                        ToastLevel.Info,
                        $"Increased quantity of {productName}",
                        "Cart",
                        ToastIcon.Info,
                        ToastPosition.BottomRight);
                }
            }
            finally
            {
                _isAddingToCart = false;
                await PersistCartAsync();
            }
        }

        private async Task AddVariantToCart(ProcessCart payload)
        {
            var getCart = _myCarts.FirstOrDefault(x => x.ProductId == payload.ProductId && x.VariantId == payload.VariantId);
            if (getCart is null)
            {
                _myCarts.Add(payload);
            }
            else
            {
                getCart.Quantity += payload.Quantity;
            }

            var productResult = await this.ProductService.GetByIdAsync(payload.ProductId);
            var name = productResult.Data?.Name ?? "Product";
            this.ToastService.ShowToast(
                ToastLevel.Success,
                $"[{name}] size {payload.SizeValue} added to cart",
                "Cart",
                ToastIcon.Success,
                ToastPosition.BottomRight);

            await PersistCartAsync();
        }

        private async Task PersistCartAsync()
        {
            await this.CookieStorageService.SetAsync(Constant.Cart.Name, JsonSerializer.Serialize(_myCarts), 30);
        }

        private void ShowDetails(GetProduct product)
        {
            this.SelectedProduct = product;
            _showModal = true;
        }

        public void CloseDetails()
        {
            _showModal = false;
        }
    }
}