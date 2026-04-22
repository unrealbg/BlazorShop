namespace BlazorShop.Web.Pages.Admin
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Seo;
    using BlazorShop.Web.Shared.Services.Contracts;
    using BlazorShop.Web.Shared.Toast;

    using Microsoft.AspNetCore.Components;

    public partial class Categories
    {
        private bool _showDialog = false;
        private bool _showEditDialog = false;
        private bool _showConfirmDeleteDialog = false;
        private Guid _categoryToDelete;
        private IEnumerable<GetCategory> _categories = Enumerable.Empty<GetCategory>();
        private CreateCategory _category = new();
        private UpdateCategory _editCategory = new();
        private UpdateCategorySeo _editCategorySeo = new();
        private bool _isCategorySeoLoading;
        private bool _isCategorySeoSaving;
        private string? _categorySeoError;
        private CategoryEditorTab _activeEditTab = CategoryEditorTab.Details;

        [Inject]
        private ICategorySeoService CategorySeoService { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            await this.GetCategories();
        }

        private async Task GetCategories()
        {
            var categoriesResult = await this.CategoryService.GetAllAsync();
            if (this.QueryFailureNotifier.TryNotifyFailure(categoriesResult, "Categories"))
            {
                _categories = [];
                return;
            }

            _categories = categoriesResult.Data ?? [];
        }

        private void AddCategory()
        {
            _showDialog = true;
        }

        private async Task StartEdit(GetCategory category)
        {
            _editCategory = new UpdateCategory { Id = category.Id, Name = category.Name };
            _activeEditTab = CategoryEditorTab.Details;
            _categorySeoError = null;
            _isCategorySeoSaving = false;
            _editCategorySeo = CreateDefaultCategorySeo(category.Id);
            _showEditDialog = true;
            await LoadCategorySeoAsync(category.Id);
        }

        private void Cancel()
        {
            _showDialog = false;
            _category = new CreateCategory();
        }

        private void CancelEdit()
        {
            _showEditDialog = false;
            _editCategory = new UpdateCategory();
            _editCategorySeo = new UpdateCategorySeo();
            _categorySeoError = null;
            _isCategorySeoLoading = false;
            _isCategorySeoSaving = false;
            _activeEditTab = CategoryEditorTab.Details;
        }

        private void ConfirmDelete(Guid id)
        {
            _categoryToDelete = id;
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

        private async Task UpdateCategoryAsync()
        {
            var result = await this.CategoryService.UpdateAsync(_editCategory);
            if (result.Success)
            {
                _showEditDialog = false;
                await this.GetCategories();
            }

            this.ShowToast(result, "Edit-Category");
        }

        private async Task LoadCategorySeoAsync(Guid categoryId)
        {
            _isCategorySeoLoading = true;
            _categorySeoError = null;

            var seoResult = await this.CategorySeoService.GetByCategoryIdAsync(categoryId);
            if (this.QueryFailureNotifier.TryNotifyFailure(seoResult, "Category SEO"))
            {
                _editCategorySeo = CreateDefaultCategorySeo(categoryId);
                _categorySeoError = seoResult.Message;
                _isCategorySeoLoading = false;
                return;
            }

            _editCategorySeo = seoResult.Data is null
                ? CreateDefaultCategorySeo(categoryId)
                : MapCategorySeo(seoResult.Data);
            _isCategorySeoLoading = false;
        }

        private async Task RetryLoadCategorySeoAsync()
        {
            if (_editCategory.Id == Guid.Empty)
            {
                return;
            }

            await LoadCategorySeoAsync(_editCategory.Id);
        }

        private async Task SaveCategorySeoAsync()
        {
            if (_isCategorySeoSaving || _editCategory.Id == Guid.Empty)
            {
                return;
            }

            _isCategorySeoSaving = true;

            try
            {
                var result = await this.CategorySeoService.UpdateAsync(_editCategory.Id, _editCategorySeo);
                if (result.Success)
                {
                    _categorySeoError = null;

                    if (result.Payload is not null)
                    {
                        _editCategorySeo = MapCategorySeo(result.Payload);
                    }
                }

                this.ShowToast(result, "Category SEO");
            }
            finally
            {
                _isCategorySeoSaving = false;
            }
        }

        private void ShowToast(ServiceResponse result, string heading)
        {
            var level = result.Success ? ToastLevel.Success : ToastLevel.Error;
            var icon = result.Success ? ToastIcon.Success : ToastIcon.Error;

            this.ToastService.ShowToast(level: level, message: result.Message, heading: heading, iconClass: icon);
        }

        private void ShowToast<TPayload>(ServiceResponse<TPayload> result, string heading)
        {
            var level = result.Success ? ToastLevel.Success : ToastLevel.Error;
            var icon = result.Success ? ToastIcon.Success : ToastIcon.Error;
            var message = string.IsNullOrWhiteSpace(result.Message)
                ? result.Success ? "Saved successfully." : "Request failed."
                : result.Message;

            this.ToastService.ShowToast(level: level, message: message, heading: heading, iconClass: icon);
        }

        private static UpdateCategorySeo CreateDefaultCategorySeo(Guid categoryId)
        {
            return new UpdateCategorySeo
            {
                CategoryId = categoryId,
                RobotsIndex = true,
                RobotsFollow = true,
                IsPublished = true,
            };
        }

        private static UpdateCategorySeo MapCategorySeo(GetCategorySeo seo)
        {
            return new UpdateCategorySeo
            {
                CategoryId = seo.CategoryId,
                Slug = seo.Slug,
                MetaTitle = seo.MetaTitle,
                MetaDescription = seo.MetaDescription,
                CanonicalUrl = seo.CanonicalUrl,
                OgTitle = seo.OgTitle,
                OgDescription = seo.OgDescription,
                OgImage = seo.OgImage,
                RobotsIndex = seo.RobotsIndex,
                RobotsFollow = seo.RobotsFollow,
                SeoContent = seo.SeoContent,
                IsPublished = seo.IsPublished,
            };
        }

        private enum CategoryEditorTab
        {
            Details,
            Seo,
        }
    }
}