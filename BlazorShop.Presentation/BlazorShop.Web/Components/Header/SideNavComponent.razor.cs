namespace BlazorShop.Web.Components.Header
{
    using BlazorShop.Web.Shared.Models.Category;

    public partial class SideNavComponent
    {
        private bool isOpen = false;

        private IEnumerable<GetCategory> _categories = Enumerable.Empty<GetCategory>();

        protected override async Task OnInitializedAsync()
        {
            var categoriesResult = await this.CategoryService.GetAllAsync();
            if (this.QueryFailureNotifier.TryNotifyFailure(categoriesResult, "Categories"))
            {
                this._categories = [];
                return;
            }

            this._categories = categoriesResult.Data ?? [];
        }

        private void OpenNav()
        {
            this.isOpen = true;
        }

        private void CloseNav()
        {
            this.isOpen = false;
        }
    }
}