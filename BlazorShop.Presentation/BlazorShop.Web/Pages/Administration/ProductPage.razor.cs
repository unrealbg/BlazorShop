namespace BlazorShop.Web.Pages.Administration
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Services.Contracts;
    using BlazorShop.Web.Shared.Toast;

    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Forms;

    public partial class ProductPage
    {
        private bool _showDialog = false;
        private bool _showConfirmDeleteDialog = false;
        private Guid _productToDelete;
        private IEnumerable<GetProduct> _products = [];
        private IEnumerable<GetCategory> _categories = [];
        private CreateProduct _product = new();
        private string? _previewImageUrl;
        private string? _producToDeleteName;

        [Inject]
        private IFileUploadService FileUploadService { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            this._categories = await this.CategoryService.GetAllAsync();
            await this.GetProducts();
        }

        private async Task GetProducts()
        {
            _products = await this.ProductService.GetAllAsync();
        }

        private void AddProduct()
        {
            _product = new CreateProduct();
            _previewImageUrl = null;
            _showDialog = true;
        }

        private void Cancel()
        {
            _showDialog = false;
            _previewImageUrl = null;
        }

        private void ConfirmDelete(Guid id)
        {
            _productToDelete = id;
            _producToDeleteName = _products.FirstOrDefault(p => p.Id == _productToDelete)?.Name;
            _showConfirmDeleteDialog = true;
        }

        private async Task DeleteProductConfirmed()
        {
            _showConfirmDeleteDialog = false;
            var result = await this.ProductService.DeleteAsync(_productToDelete);

            if (result.Success)
            {
                await this.GetProducts();
            }

            this.ShowToast(result, "Delete-Product");
        }

        private void CancelDelete()
        {
            _showConfirmDeleteDialog = false;
            _productToDelete = Guid.Empty;
        }

        private async Task SaveProduct()
        {
            if (string.IsNullOrEmpty(_product.Name))
            {
                this.ShowToast(new ServiceResponse(false, "Product name is required."), "Add-Product");
            }

            if (string.IsNullOrEmpty(_product.Image))
            {
                this.ShowToast(new ServiceResponse(false, "Please upload an image for the product."), "Add-Product");
                return;
            }

            var result = await this.ProductService.AddAsync(_product);
            if (result.Success)
            {
                _showDialog = false;
                await this.GetProducts();
            }

            this.ShowToast(result, "Add-Product");
        }

        private async Task OnFileSelected(InputFileChangeEventArgs e)
        {
            var file = e.File;

            var allowedImageFormats = new List<string>
                                          {
                                              "image/jpeg",
                                              "image/png",
                                              "image/gif",
                                              "image/bmp",
                                              "image/webp"
                                          };

            if (!allowedImageFormats.Contains(file.ContentType))
            {
                this.ToastService.ShowToast(
                    level: ToastLevel.Error,
                    message: $"Invalid file type. Only image files are allowed.",
                    heading: "Upload Image",
                    iconClass: ToastIcon.Error);
                return;
            }

            try
            {
                var result = await this.FileUploadService.UploadFileAsync(file);

                if (result.Success)
                {
                    _product.Image = result.Url;
                    _previewImageUrl = result.Url;
                }
                else
                {
                    this.ToastService.ShowToast(
                        level: ToastLevel.Error,
                        message: "Failed to upload the file.",
                        heading: "Upload Image",
                        iconClass: ToastIcon.Error);
                }
            }
            catch (Exception ex)
            {
                this.ToastService.ShowToast(
                    level: ToastLevel.Error,
                    message: ex.Message,
                    heading: "Upload Image",
                    iconClass: ToastIcon.Error);
            }
        }

        private void ShowToast(ServiceResponse result, string heading)
        {
            var level = result.Success ? ToastLevel.Success : ToastLevel.Error;
            var icon = result.Success ? ToastIcon.Success : ToastIcon.Error;

            this.ToastService.ShowToast(level: level, message: result.Message, heading: heading, iconClass: icon);
        }
    }
}