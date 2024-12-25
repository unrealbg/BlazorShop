namespace BlazorShop.Web.Pages.Products
{
    using System.Text.Json;

    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Toast;

    using Microsoft.AspNetCore.Components;

    public partial class LatestProducts
    {
        private IEnumerable<GetProduct> _products = [];

        private List<List<GetProduct>> _productGroups = new();

        private List<ProcessCart> _myCarts = [];

        private bool _isAddingToCart = false;

        private bool _showModal = false;

        [Parameter]
        public GetProduct SelectedProduct { get; set; } = new();

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
                _productGroups = _products.Where(x => x.CreatedOn.AddDays(7) >= DateTime.UtcNow)
                    .Select((product, index) => new { product, index }).GroupBy(x => x.index / 4)
                    .Select(g => g.Select(v => v.product).ToList()).ToList();
            }
        }

        private async Task AddItemToCart(Guid productId)
        {
            if (_isAddingToCart) return;

            try
            {
                _isAddingToCart = true;

                var getCart = _myCarts.FirstOrDefault(x => x.ProductId == productId);
                var productName = (await this.ProductService.GetByIdAsync(productId)).Name;

                if (getCart == null)
                {
                    _myCarts.Add(new ProcessCart { ProductId = productId, Quantity = 1 });

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
            }
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