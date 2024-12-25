namespace BlazorShop.Web.Components.Header
{
    using BlazorShop.Web.Shared.Models.Product;

    using Microsoft.AspNetCore.Components;

    public partial class SearchBarComponent
    {
        [Parameter]
        public IEnumerable<GetProduct> Products { get; set; } = Enumerable.Empty<GetProduct>();

        private GetProduct _selectedProduct = new();

        public GetProduct SelectedProduct
        {
            get => this._selectedProduct;
            set
            {
                if (this._selectedProduct != value)
                {
                    this._selectedProduct = value;
                    this.OnSelectedProductChanged();
                }
            }
        }

        private void OnSelectedProductChanged()
        {
            if (this._selectedProduct == null)
            {
                return;
            }

            this.NavigationManager.NavigateTo($"search-result/{this._selectedProduct.Name}");
        }

        private Task<IEnumerable<GetProduct>> SearchProducts(string searchText)
        {
            var filtered = this.Products.Where(
                x => x.Name!.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) || x.Description!.Contains(
                         searchText,
                         StringComparison.CurrentCultureIgnoreCase));

            return Task.FromResult(filtered);
        }
    }
}