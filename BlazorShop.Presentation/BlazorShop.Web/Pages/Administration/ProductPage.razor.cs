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
        private bool _addStep2 = false;
        private Guid _newProductId;
        private List<GetProductVariant> _newVariants = new();
        private CreateOrUpdateProductVariant _newVariantForm = new() { SizeScale = 1, SizeValue = "XS", Stock = 0 };

        private bool _showEditDialog = false;
        private bool _showConfirmDeleteDialog = false;
        private Guid _productToDelete;
        private IEnumerable<GetProduct> _products = [];
        private IEnumerable<GetCategory> _categories = [];
        private CreateProduct _product = new();
        private UpdateProduct _editProduct = new();
        private string? _previewImageUrl;
        private string? _producToDeleteName;

        private List<GetProductVariant> _variants = [];
        private CreateOrUpdateProductVariant _variantForm = new();
        private Guid? _editingVariantId;

        [Inject]
        private IFileUploadService FileUploadService { get; set; } = default!;

        [Inject]
        private IProductVariantService ProductVariantService { get; set; } = default!;

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
            _addStep2 = false;
            _newProductId = Guid.Empty;
            _newVariants.Clear();
            _newVariantForm = new CreateOrUpdateProductVariant { SizeScale = 1, SizeValue = GetSizeOptions(1).FirstOrDefault() ?? "XS", Stock = 0 };
            _showDialog = true;
        }

        private async Task LoadVariantsAsync(Guid productId)
        {
            _variants = (await this.ProductVariantService.GetByProductIdAsync(productId)).ToList();
            _variantForm = new CreateOrUpdateProductVariant
            {
                ProductId = productId,
                SizeScale = _variantForm.SizeScale == 0 ? 1 : _variantForm.SizeScale,
                SizeValue = GetSizeOptions(_variantForm.SizeScale == 0 ? 1 : _variantForm.SizeScale).FirstOrDefault() ?? string.Empty,
                Stock = 0,
            };
            _editingVariantId = null;
        }

        private async Task StartEdit(GetProduct product)
        {
            _editProduct = new UpdateProduct
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Image = product.Image,
                Price = product.Price,
                Quantity = product.Quantity,
                CategoryId = product.CategoryId
            };

            _previewImageUrl = product.Image;

            _showEditDialog = true;
            await LoadVariantsAsync(product.Id);
        }

        private void Cancel()
        {
            _showDialog = false;
            _addStep2 = false;
            _newProductId = Guid.Empty;
            _previewImageUrl = null;
        }

        private void CancelEdit()
        {
            _showEditDialog = false;
            _previewImageUrl = null;
            _variants.Clear();
            _editingVariantId = null;
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
                await this.GetProducts();
                _newProductId = result.Id ?? Guid.Empty;
                _addStep2 = true;
                _newVariantForm.ProductId = _newProductId;
            }

            this.ShowToast(result, "Add-Product");
        }

        private async Task FinishAdd()
        {
            _showDialog = false;
            _addStep2 = false;
            _newProductId = Guid.Empty;
            _newVariants.Clear();
            await this.GetProducts();
        }

        private void OnScaleChanged(ChangeEventArgs e)
        {
            if (int.TryParse(e?.Value?.ToString(), out var scale))
            {
                _variantForm.SizeScale = scale;
                _variantForm.SizeValue = GetSizeOptions(scale).FirstOrDefault() ?? string.Empty;
            }
        }

        private void OnScaleChangedAdd(ChangeEventArgs e)
        {
            if (int.TryParse(e?.Value?.ToString(), out var scale))
            {
                _newVariantForm.SizeScale = scale;
                _newVariantForm.SizeValue = GetSizeOptions(scale).FirstOrDefault() ?? string.Empty;
            }
        }

        private async Task AddVariantAsync()
        {
            if (_editProduct.Id == Guid.Empty)
            {
                this.ToastService.ShowToast(ToastLevel.Error, "Save product first.", "Variant");
                return;
            }

            var response = await this.ProductVariantService.AddAsync(_editProduct.Id, _variantForm);
            this.ShowToast(response, "Add-Variant");
            if (response.Success)
            {
                await LoadVariantsAsync(_editProduct.Id);
            }
        }

        private async Task AddVariantForNewAsync()
        {
            if (_newProductId == Guid.Empty)
            {
                this.ToastService.ShowToast(ToastLevel.Error, "Save product first.", "Variant");
                return;
            }

            var response = await this.ProductVariantService.AddAsync(_newProductId, _newVariantForm);
            this.ShowToast(response, "Add-Variant");
            if (response.Success)
            {
                var list = await this.ProductVariantService.GetByProductIdAsync(_newProductId);
                _newVariants = list.ToList();
                _newVariantForm = new CreateOrUpdateProductVariant { ProductId = _newProductId, SizeScale = 1, SizeValue = GetSizeOptions(1).FirstOrDefault() ?? "XS", Stock = 0 };
            }
        }

        private void BeginVariantEdit(GetProductVariant v)
        {
            _editingVariantId = v.Id;
            _variantForm = new CreateOrUpdateProductVariant
            {
                Id = v.Id,
                ProductId = v.ProductId,
                Sku = v.Sku,
                SizeScale = v.SizeScale,
                SizeValue = v.SizeValue,
                Price = v.Price,
                Stock = v.Stock,
                Color = v.Color,
                IsDefault = v.IsDefault
            };
        }

        private async Task UpdateVariantAsync()
        {
            if (_editingVariantId is null)
            {
                return;
            }

            var response = await this.ProductVariantService.UpdateAsync(_variantForm);
            this.ShowToast(response, "Update-Variant");
            if (response.Success)
            {
                await LoadVariantsAsync(_editProduct.Id);
            }
        }

        private void CancelVariantEdit()
        {
            _editingVariantId = null;
            _variantForm = new CreateOrUpdateProductVariant
            {
                ProductId = _editProduct.Id,
                SizeScale = 1,
                SizeValue = GetSizeOptions(1).FirstOrDefault() ?? string.Empty
            };
        }

        private async Task DeleteVariantAsync(Guid id)
        {
            var response = await this.ProductVariantService.DeleteAsync(id);
            this.ShowToast(response, "Delete-Variant");
            if (response.Success)
            {
                await LoadVariantsAsync(_editProduct.Id);
            }
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
                    if (!_addStep2)
                    {
                        _product.Image = result.Url;
                    }
                    else
                    {
                        _editProduct.Image = result.Url;
                    }
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

        private async Task OnEditFileSelected(InputFileChangeEventArgs e)
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
                    _editProduct.Image = result.Url;
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

        private IEnumerable<string> GetSizeOptions(int scale) => scale switch
        {
            1 => new[] { "XS", "S", "M", "L", "XL", "XXL" },
            2 => Enumerable.Range(36, 20).Select(x => x.ToString()),
            10 => Enumerable.Range(35, 14).Select(x => x.ToString()),
            11 => new[] { "5", "5.5", "6", "6.5", "7", "7.5", "8", "8.5", "9", "9.5", "10", "10.5", "11", "11.5", "12" },
            12 => new[] { "4", "4.5", "5", "5.5", "6", "6.5", "7", "7.5", "8", "8.5", "9", "9.5", "10", "10.5", "11" },
            _ => Array.Empty<string>()
        };

        private string ScaleLabel(int scale) => scale switch
        {
            1 => "Clothing",
            2 => "Clothing EU",
            10 => "Shoes EU",
            11 => "Shoes US",
            12 => "Shoes UK",
            _ => "—"
        };

        private void ShowToast(ServiceResponse result, string heading)
        {
            var level = result.Success ? ToastLevel.Success : ToastLevel.Error;
            var icon = result.Success ? ToastIcon.Success : ToastIcon.Error;

            this.ToastService.ShowToast(level: level, message: result.Message, heading: heading, iconClass: icon);
        }

        private async Task UpdateProductAsync()
        {
            if (string.IsNullOrEmpty(_editProduct.Name))
            {
                this.ShowToast(new ServiceResponse(false, "Product name is required."), "Edit-Product");
            }

            if (string.IsNullOrEmpty(_editProduct.Image))
            {
                this.ShowToast(new ServiceResponse(false, "Please upload an image for the product."), "Edit-Product");
                return;
            }

            var result = await this.ProductService.UpdateAsync(_editProduct);
            if (result.Success)
            {
                _showEditDialog = false;
                await this.GetProducts();
            }

            this.ShowToast(result, "Edit-Product");
        }

        private void CancelDelete()
        {
            _showConfirmDeleteDialog = false;
            _productToDelete = Guid.Empty;
        }
    }
}