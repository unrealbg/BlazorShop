namespace BlazorShop.Web.Pages.Administration
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Toast;

    public partial class CategoryPage
    {
        private bool _showDialog = false;
        private bool _showConfirmDeleteDialog = false;
        private Guid _categoryToDelete;
        private IEnumerable<GetCategory> _categories = Enumerable.Empty<GetCategory>();
        private CreateCategory _category = new();
        private string? _categoryToDeleteName;

        protected override async Task OnInitializedAsync()
        {
            await this.GetCategories();
        }

        private async Task GetCategories()
        {
            try
            {
                _categories = await this.CategoryService.GetAllAsync();
            }
            catch
            {
                this.ToastService.ShowToast(ToastLevel.Error, "Failed to load categories.", "Error", ToastIcon.Error);
            }
        }

        private void AddCategory()
        {
            _showDialog = true;
        }

        private void Cancel()
        {
            _showDialog = false;
            _category = new CreateCategory();
        }

        private void ConfirmDelete(Guid id)
        {
            _categoryToDelete = id;
            _categoryToDeleteName = _categories.FirstOrDefault(c => c.Id == _categoryToDelete)?.Name;
            _showConfirmDeleteDialog = true;
        }

        private async Task DeleteCategoryConfirmed()
        {
            _showConfirmDeleteDialog = false;
            var result = await this.CategoryService.DeleteAsync(_categoryToDelete);

            if (result.Success)
            {
                await this.GetCategories();
            }

            this.ShowToast(result, "Delete-Category");
        }

        private void CancelDelete()
        {
            _showConfirmDeleteDialog = false;
            _categoryToDelete = Guid.Empty;
        }

        private async Task SaveCategory()
        {
            var result = await this.CategoryService.AddAsync(_category);
            if (result.Success)
            {
                _showDialog = false;
                await this.GetCategories();
            }

            this.ShowToast(result, "Add-Category");
        }

        private void ShowToast(ServiceResponse result, string heading)
        {
            var level = result.Success ? ToastLevel.Success : ToastLevel.Error;
            var icon = result.Success ? ToastIcon.Success : ToastIcon.Error;

            this.ToastService.ShowToast(level: level, message: result.Message, heading: heading, iconClass: icon);
        }
    }
}