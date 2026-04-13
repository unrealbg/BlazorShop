namespace BlazorShop.Web.Layout
{
    using BlazorShop.Web.Shared.Models.Product;

    public partial class Header
    {
        protected IEnumerable<GetProduct> Products = new List<GetProduct>();
        private bool _showCategories;

        protected override async Task OnInitializedAsync()
        {
            var productsResult = await this.ProductService.GetAllAsync();
            if (this.QueryFailureNotifier.TryNotifyFailure(productsResult, "Products"))
            {
                this.Products = [];
                return;
            }

            this.Products = productsResult.Data ?? [];
        }

        private void ToggleCategories() => _showCategories = !_showCategories;
    }
}