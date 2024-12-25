namespace BlazorShop.Web.Pages.Public
{
    using System.Text.Json;

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
            }

            var cartJson = await this.CookieStorageService.GetAsync(Constant.Cart.Name);

            if (!string.IsNullOrEmpty(cartJson))
            {
                _myCarts = JsonSerializer.Deserialize<List<ProcessCart>>(cartJson) ?? new List<ProcessCart>();
            }

            this.SelectedCategory = await this.CategoryService.GetByIdAsync(Guid.Parse(this.CategoryId));
            _productsByCategory = await this.CategoryService.GetProductsByCategoryAsync(Guid.Parse(this.CategoryId));
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