namespace BlazorShop.Web.Components.Search
{
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Toast;

    using Microsoft.AspNetCore.Components;
    using System.Text.Json;

    using BlazorShop.Web.Shared;

    public partial class SearchResult : IAsyncDisposable
    {
        private IEnumerable<GetProduct> _searchedProducts = [];
        private List<ProcessCart> _myCarts = new();
        private bool _isAddingToCart = false;

        [Parameter]
        public string Filter { get; set; } = string.Empty;

        protected override async Task OnParametersSetAsync()
        {
            if (string.IsNullOrEmpty(this.Filter))
            {
                this.NavigationManager.NavigateTo("/");
            }

            var products = await this.ProductService.GetAllAsync();

            if (products.Any())
            {
                var searchWords = this.Filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                this._searchedProducts = products.Where(
                        x => searchWords.Any(word => x.Name!.Contains(word, StringComparison.OrdinalIgnoreCase))
                             || searchWords.Any(word => x.Description!.Contains(word, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
        }

        private async Task AddItemToCart(Guid productId)
        {
            if (_isAddingToCart) return;

            try
            {
                _isAddingToCart = true;

                var getCart = _myCarts.FirstOrDefault(x => x.ProductId == productId);
                var productName = (await ProductService.GetByIdAsync(productId)).Name;

                if (getCart == null)
                {
                    _myCarts.Add(new ProcessCart
                                     {
                                         ProductId = productId,
                                         Quantity = 1
                                     });

                    ToastService.ShowToast(ToastLevel.Success, $"Product {productName} added to cart", "Cart", ToastIcon.Success, ToastPosition.BottomRight);
                }
                else
                {
                    getCart.Quantity++;
                    ToastService.ShowToast(ToastLevel.Info, $"Increased quantity of {productName}", "Cart", ToastIcon.Info, ToastPosition.BottomRight);
                }
            }
            finally
            {
                _isAddingToCart = false;
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