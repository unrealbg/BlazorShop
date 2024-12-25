namespace BlazorShop.Web.Pages.Public.Deals
{
    using System.Text.Json;

    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Toast;

    using Microsoft.AspNetCore.Components;

    public partial class TodaysDeals
    {
        private IEnumerable<GetProduct> _todaysDeals = [];

        private bool _isAddingToCart = false;

        private List<ProcessCart> _myCarts = [];

        private bool _showModal = false;

        [Parameter]
        public GetProduct SelectedProduct { get; set; } = new();

        protected override async Task OnInitializedAsync()
        {
            try
            {
                _todaysDeals = (await this.ProductService.GetAllAsync()).OrderBy(p => p.Price).Take(10);
            }
            catch
            {
                this.ToastService.ShowToast(ToastLevel.Error, "Failed to load today's deals.", "Error");
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