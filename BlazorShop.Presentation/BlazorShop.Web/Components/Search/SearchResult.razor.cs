namespace BlazorShop.Web.Components.Search
{
    using BlazorShop.Web.Services;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Toast;

    using Microsoft.AspNetCore.Components;
    using System.Text.Json;

    using BlazorShop.Web.Shared;

    public partial class SearchResult : IAsyncDisposable
    {
        private IEnumerable<GetCatalogProduct> _searchedProducts = [];
        private List<ProcessCart> _myCarts = new();
        private bool _isAddingToCart = false;

        private bool _showModal = false;
        [Parameter]
        public GetProduct SelectedProduct { get; set; } = new();

        [Parameter]
        public string Filter { get; set; } = string.Empty;

        protected override async Task OnParametersSetAsync()
        {
            if (string.IsNullOrEmpty(this.Filter))
            {
                this.NavigationManager.NavigateTo("/");
                return;
            }

            var productsResult = await this.ProductService.GetCatalogPageAsync(new ProductCatalogQuery
            {
                PageNumber = 1,
                PageSize = 60,
                SearchTerm = this.Filter,
                SortBy = ProductCatalogSortBy.NameAscending,
            });
            if (this.QueryFailureNotifier.TryNotifyFailure(productsResult, "Search"))
            {
                this._searchedProducts = [];
                return;
            }

            this._searchedProducts = productsResult.Data?.Items ?? [];
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

        private async Task ShowDetailsAsync(Guid productId)
        {
            var productResult = await this.ProductService.GetByIdAsync(productId);
            if (this.QueryFailureNotifier.TryNotifyFailure(productResult, "Product details") || productResult.Data is null)
            {
                return;
            }

            SelectedProduct = productResult.Data;
            _showModal = true;
        }

        private void CloseDetails()
        {
            _showModal = false;
        }

        private async Task AddItemToCart(Guid productId)
        {
            if (_isAddingToCart) return;

            try
            {
                _isAddingToCart = true;

                var getCart = _myCarts.FirstOrDefault(x => x.ProductId == productId && x.VariantId == null);
                var productResult = await ProductService.GetByIdAsync(productId);
                if (this.QueryFailureNotifier.TryNotifyFailure(productResult, "Cart", ToastPosition.BottomRight) ||
                    productResult.Data is null)
                {
                    return;
                }

                var product = productResult.Data;
                var productName = product.Name;

                if (getCart == null)
                {
                    _myCarts.Add(new ProcessCart
                                     {
                                         ProductId = productId,
                                         Quantity = 1,
                                         UnitPrice = product.Price
                                     });
                    NotificationService.NotifyCartItemAdded(productName, ToastPosition.BottomRight);
                }
                else
                {
                    getCart.Quantity++;
                    NotificationService.NotifyCartQuantityIncreased(productName, ToastPosition.BottomRight);
                }
            }
            finally
            {
                _isAddingToCart = false;
            }
        }

        private void AddVariantToCart(ProcessCart payload)
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
        }

        public async ValueTask DisposeAsync()
        {
            if (_myCarts != null && _myCarts.Any())
            {
                await CookieStorageService.SetAsync(Constant.Cart.Name, JsonSerializer.Serialize(_myCarts), 30, "/");
            }
        }
    }
}