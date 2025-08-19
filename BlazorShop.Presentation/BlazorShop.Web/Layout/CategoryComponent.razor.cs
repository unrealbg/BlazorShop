namespace BlazorShop.Web.Layout
{
    using BlazorShop.Web.Shared.Models.Category;
    using Microsoft.AspNetCore.Components;

    public partial class CategoryComponent
    {
        private IEnumerable<GetCategory> _categories = [];

        [Parameter]
        public EventCallback OnCategorySelected { get; set; }

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

        private (string bg, string ring) GetColor(int index)
        {
            var palette = new (string bg, string ring)[]
            {
                ("bg-rose-500","ring-rose-200"),
                ("bg-amber-500","ring-amber-200"),
                ("bg-emerald-500","ring-emerald-200"),
                ("bg-sky-500","ring-sky-200"),
                ("bg-violet-500","ring-violet-200"),
                ("bg-fuchsia-500","ring-fuchsia-200"),
            };
            return palette[index % palette.Length];
        }

        private async Task SelectCategory(Guid categoryId)
        {
            this.NavigationManager.NavigateTo($"main/products/category/{categoryId}");
            if (OnCategorySelected.HasDelegate)
            {
                await OnCategorySelected.InvokeAsync();
            }
        }
    }
}