namespace BlazorShop.Web.Layout
{
    using BlazorShop.Web.Shared.Models.Product;

    public partial class Header
    {
        protected IEnumerable<GetProduct> Products = new List<GetProduct>();
        private bool _showCategories;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                this.Products = await this.ProductService.GetAllAsync();
            }
            catch
            {
                // handle error
            }
        }

        private void ToggleCategories() => _showCategories = !_showCategories;
    }
}