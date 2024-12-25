namespace BlazorShop.Web.Layout
{
    using BlazorShop.Web.Shared.Models.Category;

    public partial class CategoryComponent
    {
        private IEnumerable<GetCategory> _categories = [];

        protected override async Task OnInitializedAsync()
        {
            try
            {
                this._categories = await this.CategoryService.GetAllAsync();
            }
            catch
            {
                this.ToastService.ShowErrorToast("An error occurred while loading categories.");
            }
        }
    }
}