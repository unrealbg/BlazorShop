namespace BlazorShop.Web.Pages.Public
{
    using System.Text.Json;

    using BlazorShop.Web.Services;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Toast;

    using Microsoft.AspNetCore.Components;

    public partial class MainPage
    {
        private IEnumerable<GetProduct> _productsByCategory = [];

        private List<ProcessCart> _myCarts = new();

        private bool _isAddingToCart = false;

        private bool _showModal = false;

        [Parameter]
        public GetProduct SelectedProduct { get; set; } = new();

        [Parameter]
        public string CategoryId { get; set; } = string.Empty;

        public GetCategory SelectedCategory { get; set; } = new();

        protected override async Task OnParametersSetAsync()
        {
            if (string.IsNullOrEmpty(this.CategoryId))
            {
                this.NavigationManager.NavigateTo("/");
                return;
            }

            var cartJson = await this.CookieStorageService.GetAsync(Constant.Cart.Name);

            if (!string.IsNullOrEmpty(cartJson))
            {
                _myCarts = JsonSerializer.Deserialize<List<ProcessCart>>(cartJson) ?? new List<ProcessCart>();
            }

            var categoryId = Guid.Parse(this.CategoryId);
            var categoryResult = await this.CategoryService.GetByIdAsync(categoryId);
            if (this.QueryFailureNotifier.TryNotifyFailure(categoryResult, "Categories") || categoryResult.Data is null)
            {
                this.SelectedCategory = new();
                _productsByCategory = [];
                return;
            }

            this.SelectedCategory = categoryResult.Data;

            var productsResult = await this.CategoryService.GetProductsByCategoryAsync(categoryId);
            if (this.QueryFailureNotifier.TryNotifyFailure(productsResult, "Products"))
            {
                _productsByCategory = [];
                return;
            }

            _productsByCategory = productsResult.Data ?? [];
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
                    this.NotificationService.NotifyCartItemAdded(productName, ToastPosition.BottomRight);
                }
                else
                {
                    getCart.Quantity++;
                    this.NotificationService.NotifyCartQuantityIncreased(productName, ToastPosition.BottomRight);
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
            this.NotificationService.NotifyCartVariantAdded(name, payload.SizeValue, ToastPosition.BottomRight);

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

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}