namespace BlazorShop.Web.Pages.Products
{
    using System.Text.Json;

    using BlazorShop.Web.Services;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Toast;

    using Microsoft.AspNetCore.Components;

    public partial class NewReleases
    {
        private IEnumerable<GetCatalogProduct> _newReleases = [];

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

            var allProductsResult = await this.ProductService.GetCatalogPageAsync(new ProductCatalogQuery
            {
                PageNumber = 1,
                PageSize = 60,
                SortBy = ProductCatalogSortBy.Newest,
                CreatedAfterUtc = DateTime.UtcNow.AddDays(-7),
            });
            if (this.QueryFailureNotifier.TryNotifyFailure(allProductsResult, "Products"))
            {
                _newReleases = [];
                return;
            }

            _newReleases = allProductsResult.Data?.Items ?? [];
        }

        private async Task HandleAddToCart(GetCatalogProduct product)
        {
            if (product.HasVariants)
            {
                await ShowDetailsAsync(product.Id);
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

        private async Task ShowDetailsAsync(Guid productId)
        {
            var productResult = await this.ProductService.GetByIdAsync(productId);
            if (this.QueryFailureNotifier.TryNotifyFailure(productResult, "Product details") || productResult.Data is null)
            {
                return;
            }

            this.SelectedProduct = productResult.Data;
            _showModal = true;
        }

        public void CloseDetails()
        {
            _showModal = false;
        }
    }
}