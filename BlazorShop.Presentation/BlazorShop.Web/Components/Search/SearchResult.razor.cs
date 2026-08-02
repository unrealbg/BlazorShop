namespace BlazorShop.Web.Components.Search
{
    using BlazorShop.Web.Services;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Toast;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Web;
    using System.Text.Json;

    using BlazorShop.Web.Shared;

    public partial class SearchResult : IAsyncDisposable
    {
        private IReadOnlyList<GetCatalogProduct> _searchedProducts = [];
        private IReadOnlyList<GetCategory> _categories = [];
        private List<ProcessCart> _myCarts = new();
        private bool _isAddingToCart = false;
        private bool _isLoading;
        private bool _categoriesLoaded;
        private string? _loadError;
        private string _searchInput = string.Empty;
        private ProductCatalogSortBy _selectedSort = ProductCatalogSortBy.NameAscending;

        private bool _showModal = false;
        [Parameter]
        public GetProduct SelectedProduct { get; set; } = new();

        [Parameter]
        public string Filter { get; set; } = string.Empty;

        [Parameter]
        [SupplyParameterFromQuery(Name = "sort")]
        public string? Sort { get; set; }

        [Parameter]
        [SupplyParameterFromQuery(Name = "category")]
        public Guid? CategoryId { get; set; }

        protected override async Task OnParametersSetAsync()
        {
            if (string.IsNullOrWhiteSpace(this.Filter))
            {
                this.NavigationManager.NavigateTo("/");
                return;
            }

            _searchInput = this.Filter.Trim();
            _selectedSort = Enum.TryParse<ProductCatalogSortBy>(Sort, ignoreCase: true, out var parsedSort)
                ? parsedSort
                : ProductCatalogSortBy.NameAscending;
            _isLoading = true;
            _loadError = null;

            if (!_categoriesLoaded)
            {
                var categoriesResult = await this.CategoryService.GetAllAsync();
                _categories = categoriesResult.Success
                    ? (categoriesResult.Data ?? []).OrderBy(category => category.Name).ToArray()
                    : [];
                _categoriesLoaded = true;
            }

            var productsResult = await this.ProductService.GetCatalogPageAsync(new ProductCatalogQuery
            {
                PageNumber = 1,
                PageSize = 60,
                SearchTerm = _searchInput,
                SortBy = _selectedSort,
                CategoryId = CategoryId,
            });
            if (this.QueryFailureNotifier.TryNotifyFailure(productsResult, "Search"))
            {
                this._searchedProducts = [];
                _loadError = "We couldn't load the matching products right now. Please try again.";
                _isLoading = false;
                return;
            }

            this._searchedProducts = (productsResult.Data?.Items ?? []).ToArray();
            _isLoading = false;
        }

        private void ApplySearch()
        {
            if (!string.IsNullOrWhiteSpace(_searchInput))
            {
                NavigateToCurrentState(_searchInput, _selectedSort, CategoryId);
            }
        }

        private void HandleSearchKeyDown(KeyboardEventArgs args)
        {
            if (string.Equals(args.Key, "Enter", StringComparison.Ordinal))
            {
                ApplySearch();
            }
        }

        private void ChangeSort(ChangeEventArgs args)
        {
            var sort = Enum.TryParse<ProductCatalogSortBy>(args.Value?.ToString(), out var parsedSort)
                ? parsedSort
                : ProductCatalogSortBy.NameAscending;

            NavigateToCurrentState(Filter, sort, CategoryId);
        }

        private void ChangeCategory(ChangeEventArgs args)
        {
            var categoryId = Guid.TryParse(args.Value?.ToString(), out var parsedCategoryId)
                ? parsedCategoryId
                : (Guid?)null;

            NavigateToCurrentState(Filter, _selectedSort, categoryId);
        }

        private void ClearFilters()
        {
            NavigateToCurrentState(Filter, ProductCatalogSortBy.NameAscending, null);
        }

        private void NavigateToCurrentState(
            string searchTerm,
            ProductCatalogSortBy sortBy,
            Guid? categoryId)
        {
            NavigationManager.NavigateTo(SearchCatalogUrl.Build(searchTerm, sortBy, categoryId));
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
