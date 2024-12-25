namespace BlazorShop.Web.Components.Header
{
    using BlazorShop.Web.Shared.Models.Category;

    public partial class SideNavComponent
    {
        private bool isOpen = false;

        private IEnumerable<GetCategory> _categories = Enumerable.Empty<GetCategory>();

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