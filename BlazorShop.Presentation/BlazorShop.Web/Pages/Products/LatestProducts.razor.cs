namespace BlazorShop.Web.Pages.Products
{
    using System.Text.Json;
    using System.Threading;

    using BlazorShop.Web.Services;
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Toast;

    using Microsoft.AspNetCore.Components;

    public partial class LatestProducts : IAsyncDisposable
    {
        private readonly List<GetCatalogProduct> _latestProducts = new();

        private List<ProcessCart> _myCarts = [];

        private bool _isAddingToCart = false;

        private bool _showModal = false;
        private bool _showCategories = false;

        private const int PageSize = 3;
        private int _currentPage = 0;
        private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)_latestProducts.Count / PageSize));
        private IEnumerable<GetCatalogProduct> CurrentPageItems => _latestProducts.Skip(_currentPage * PageSize).Take(PageSize);

        private PeriodicTimer? _autoTimer;
        private Task? _autoSlideTask;

        [Parameter]
        public GetProduct SelectedProduct { get; set; } = new();

        private void ToggleCategories() => _showCategories = !_showCategories;

        protected override async Task OnInitializedAsync()
        {
            var cartJson = await this.CookieStorageService.GetAsync(Constant.Cart.Name);

            if (!string.IsNullOrEmpty(cartJson))
            {
                _myCarts = JsonSerializer.Deserialize<List<ProcessCart>>(cartJson) ?? new List<ProcessCart>();
            }

            var productsResult = await this.ProductService.GetCatalogPageAsync(new ProductCatalogQuery
            {
                PageNumber = 1,
                PageSize = 12,
                SortBy = ProductCatalogSortBy.Newest,
            });
            if (this.QueryFailureNotifier.TryNotifyFailure(productsResult, "Products"))
            {
                _latestProducts.Clear();
                return;
            }

            var products = productsResult.Data?.Items ?? [];

            if (products.Any())
            {
                foreach (var p in products)
                {
                    _latestProducts.Add(p);
                }

                StartAutoSlide();
            }
        }

        private void NextPage()
        {
            if (_latestProducts.Count == 0) return;
            _currentPage = (_currentPage + 1) % TotalPages;
        }

        private void PrevPage()
        {
            if (_latestProducts.Count == 0) return;
            _currentPage = (_currentPage - 1 + TotalPages) % TotalPages;
        }

        private void GoToPage(int index)
        {
            if (_latestProducts.Count == 0) return;
            if (index < 0 || index >= TotalPages) return;
            _currentPage = index;
        }

        private void StartAutoSlide()
        {
            _autoTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            _autoSlideTask = Task.Run(async () =>
            {
                try
                {
                    while (await _autoTimer!.WaitForNextTickAsync())
                    {
                        await InvokeAsync(() =>
                        {
                            NextPage();
                            StateHasChanged();
                        });
                    }
                }
                catch
                {
                    // ignored
                }
            });
        }

        private void PauseAutoSlide()
        {
            _autoTimer?.Dispose();
            _autoTimer = null;
        }

        private void ResumeAutoSlide()
        {
            if (_autoTimer is null)
            {
                StartAutoSlide();
            }
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

        public async ValueTask DisposeAsync()
        {
            _autoTimer?.Dispose();
            if (_myCarts != null && _myCarts.Any())
            {
                await this.CookieStorageService.SetAsync(
                    Constant.Cart.Name,
                    JsonSerializer.Serialize(_myCarts),
                    30,
                    "/");
            }
        }
    }
}