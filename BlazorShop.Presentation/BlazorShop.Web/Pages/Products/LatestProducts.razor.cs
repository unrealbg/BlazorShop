namespace BlazorShop.Web.Pages.Products
{
    using System.Text.Json;
    using System.Threading;

    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Toast;

    using Microsoft.AspNetCore.Components;

    public partial class LatestProducts : IAsyncDisposable
    {
        private IEnumerable<GetProduct> _products = [];

        private readonly List<GetProduct> _latestProducts = new();

        private List<ProcessCart> _myCarts = [];

        private bool _isAddingToCart = false;

        private bool _showModal = false;
        private bool _showCategories = false;

        private const int PageSize = 3;
        private int _currentPage = 0;
        private int TotalPages => Math.Max(1, (int)Math.Ceiling((double)_latestProducts.Count / PageSize));
        private IEnumerable<GetProduct> CurrentPageItems => _latestProducts.Skip(_currentPage * PageSize).Take(PageSize);

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

            try
            {
                _products = await this.ProductService.GetAllAsync();
            }
            catch
            {
                this.ToastService.ShowToast(
                    ToastLevel.Error,
                    "An error occurred while loading products",
                    "Error",
                    ToastIcon.Error,
                    ToastPosition.BottomRight);
            }

            if (_products.Any())
            {
                foreach (var p in _products.OrderByDescending(p => p.CreatedOn).Take(12))
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
                var product = await this.ProductService.GetByIdAsync(productId);
                var productName = product.Name;

                if (getCart == null)
                {
                    _myCarts.Add(new ProcessCart { ProductId = productId, Quantity = 1, UnitPrice = product.Price });

                    this.ToastService.ShowToast(
                        ToastLevel.Success,
                        $"[{productName}] added to cart",
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

            var name = (await this.ProductService.GetByIdAsync(payload.ProductId)).Name;
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