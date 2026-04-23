namespace BlazorShop.Web.Pages.Admin
{
    using BlazorShop.Web.Services;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Category;
    using BlazorShop.Web.Shared.Models.Product;
    using BlazorShop.Web.Shared.Models.Seo;
    using BlazorShop.Web.Shared.Services.Contracts;
    using BlazorShop.Web.Shared.Toast;
    using Microsoft.AspNetCore.Components;
    using Microsoft.AspNetCore.Components.Forms;

    public partial class Products
    {
        private readonly AsyncActionGate _saveProductGate = new();
        private readonly AsyncActionGate _updateProductGate = new();
        private readonly AsyncActionGate _deleteProductGate = new();
        private readonly AsyncActionGate _variantGate = new();
        private bool _showDialog = false;
        private bool _addStep2 = false;
        private Guid _newProductId;
        private List<GetProductVariant> _newVariants = new();
        private string? _newVariantsError;
        private CreateOrUpdateProductVariant _newVariantForm = new() { SizeScale = 1, SizeValue = "XS", Stock = 0 };

        private bool _showEditDialog = false;
        private bool _showConfirmDeleteDialog = false;
        private Guid _productToDelete;
        private IEnumerable<GetProduct> _products = [];
        private IEnumerable<GetCategory> _categories = [];
        private bool _isProductsLoading;
        private string? _productsError;
        private string? _categoriesError;
        private CreateProduct _product = new();
        private UpdateProduct _editProduct = new();
        private UpdateProductSeo _editProductSeo = new();
        private string? _previewImageUrl;
        private string? _producToDeleteName;
        private bool _isUploadingImage;
        private bool _isProductSeoLoading;
        private bool _isProductSeoSaving;
        private string? _productSeoError;
        private ProductEditorTab _activeEditTab = ProductEditorTab.Catalog;

        private List<GetProductVariant> _variants = [];
        private string? _variantsError;
        private CreateOrUpdateProductVariant _variantForm = new();
        private Guid? _editingVariantId;

        private string _query = string.Empty;
        private bool _allExpanded = true;
        private HashSet<Guid> _expandedCats = new();

        [Inject]
        private IFileUploadService FileUploadService { get; set; } = default!;

        [Inject]
        private IProductVariantService ProductVariantService { get; set; } = default!;

        [Inject]
        private IProductSeoService ProductSeoService { get; set; } = default!;

        private bool IsSavingProduct => _saveProductGate.IsRunning;

        private bool IsUpdatingProduct => _updateProductGate.IsRunning;

        private bool IsDeletingProduct => _deleteProductGate.IsRunning;

        private bool IsRunningVariantAction => _variantGate.IsRunning;

        protected override async Task OnInitializedAsync()
        {
            await RetryLoadCatalogAsync();
        }

        private async Task LoadCategoriesAsync()
        {
            _categoriesError = null;

            var categoriesResult = await this.CategoryService.GetAllForAdminAsync();
            if (this.QueryFailureNotifier.TryNotifyFailure(categoriesResult, "Categories", showToast: false))
            {
                _categories = [];
                _categoriesError = FeedbackMessageResolver.ResolveQueryFailure(categoriesResult, "We couldn't load categories right now. Please try again.");
                return;
            }

            _categories = categoriesResult.Data ?? [];
        }

        private async Task RetryLoadCatalogAsync()
        {
            _expandedCats.Clear();
            await LoadCategoriesAsync();
            await GetProducts();

            foreach (var c in _categories)
            {
                _expandedCats.Add(c.Id);
            }

            _allExpanded = _categories.Any();
        }

        private async Task GetProducts()
        {
            _isProductsLoading = true;
            _productsError = null;

            var productsResult = await this.ProductService.GetAllAsync();
            if (this.QueryFailureNotifier.TryNotifyFailure(productsResult, "Products", showToast: false))
            {
                _products = [];
                _productsError = FeedbackMessageResolver.ResolveQueryFailure(productsResult, "We couldn't load products right now. Please try again.");
                _isProductsLoading = false;
                return;
            }

            _products = productsResult.Data ?? [];
            _isProductsLoading = false;
        }

        private void ToggleCat(Guid id)
        {
            if (_expandedCats.Contains(id)) _expandedCats.Remove(id); else _expandedCats.Add(id);
        }

        private void ToggleAllCats()
        {
            if (_allExpanded)
            {
                _expandedCats.Clear();
                _allExpanded = false;
            }
            else
            {
                _expandedCats = _categories.Select(c => c.Id).ToHashSet();
                _allExpanded = true;
            }
        }

        private void AddProduct()
        {
            _product = new CreateProduct();
            _previewImageUrl = null;
            _addStep2 = false;
            _newProductId = Guid.Empty;
            _newVariants.Clear();
            _newVariantsError = null;
            _newVariantForm = new CreateOrUpdateProductVariant { SizeScale = 1, SizeValue = GetSizeOptions(1).FirstOrDefault() ?? "XS", Stock = 0 };
            _showDialog = true;
        }

        private async Task LoadVariantsAsync(Guid productId)
        {
            _variantsError = null;

            var variantsResult = await this.ProductVariantService.GetByProductIdAsync(productId);
            if (this.QueryFailureNotifier.TryNotifyFailure(variantsResult, "Variants", showToast: false))
            {
                _variants = [];
                _variantsError = FeedbackMessageResolver.ResolveQueryFailure(variantsResult, "We couldn't load product variants right now. Please try again.");
                return;
            }

            _variants = (variantsResult.Data ?? []).ToList();
            _variantForm = new CreateOrUpdateProductVariant
            {
                ProductId = productId,
                SizeScale = _variantForm.SizeScale == 0 ? 1 : _variantForm.SizeScale,
                SizeValue = GetSizeOptions(_variantForm.SizeScale == 0 ? 1 : _variantForm.SizeScale).FirstOrDefault() ?? string.Empty,
                Stock = 0,
            };
            _editingVariantId = null;
        }

        private async Task LoadProductSeoAsync(Guid productId)
        {
            _isProductSeoLoading = true;
            _productSeoError = null;

            var seoResult = await this.ProductSeoService.GetByProductIdAsync(productId);
            if (this.QueryFailureNotifier.TryNotifyFailure(seoResult, "Product SEO", showToast: false))
            {
                _editProductSeo = CreateDefaultProductSeo(productId);
                _productSeoError = FeedbackMessageResolver.ResolveQueryFailure(seoResult, "We couldn't load product SEO right now. Please try again.");
                _isProductSeoLoading = false;
                return;
            }

            _editProductSeo = seoResult.Data is null
                ? CreateDefaultProductSeo(productId)
                : MapProductSeo(seoResult.Data);
            _isProductSeoLoading = false;
        }

        private async Task RetryLoadProductSeoAsync()
        {
            if (_editProduct.Id == Guid.Empty)
            {
                return;
            }

            await LoadProductSeoAsync(_editProduct.Id);
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

            _activeEditTab = ProductEditorTab.Catalog;
            _productSeoError = null;
            _isProductSeoSaving = false;
            _editProductSeo = CreateDefaultProductSeo(product.Id);
            _previewImageUrl = product.Image;

            _showEditDialog = true;
            await Task.WhenAll(LoadVariantsAsync(product.Id), LoadProductSeoAsync(product.Id));
        }

        private void Cancel()
        {
            _showDialog = false;
            _addStep2 = false;
            _newProductId = Guid.Empty;
            _previewImageUrl = null;
            _isUploadingImage = false;
        }

        private void CancelEdit()
        {
            _showEditDialog = false;
            _previewImageUrl = null;
            _variants.Clear();
            _variantsError = null;
            _editingVariantId = null;
            _activeEditTab = ProductEditorTab.Catalog;
            _editProductSeo = new();
            _productSeoError = null;
            _isProductSeoLoading = false;
            _isProductSeoSaving = false;
            _isUploadingImage = false;
        }

        private void ConfirmDelete(Guid id)
        {
            _productToDelete = id;
            _producToDeleteName = _products.FirstOrDefault(p => p.Id == _productToDelete)?.Name;
            _showConfirmDeleteDialog = true;
        }

        private async Task DeleteProductConfirmed()
        {
            await _deleteProductGate.RunAsync(async () =>
            {
                _showConfirmDeleteDialog = false;
                var result = await this.ProductService.DeleteAsync(_productToDelete);

                if (result.Success)
                {
                    await this.GetProducts();
                }

                this.ShowToast(result, "Products", successFallback: "Product deleted successfully.", failureFallback: "We couldn't delete that product. Try again.");
            });
        }

        private async Task SaveProduct()
        {
            if (IsSavingProduct)
            {
                return;
            }

            if (string.IsNullOrEmpty(_product.Name))
            {
                this.ShowToast(new ServiceResponse(false, "Product name is required."), "Products");
                return;
            }

            if (string.IsNullOrEmpty(_product.Image))
            {
                this.ShowToast(new ServiceResponse(false, "Please upload an image for the product."), "Products");
                return;
            }

            await _saveProductGate.RunAsync(async () =>
            {
                var result = await this.ProductService.AddAsync(_product);
                if (result.Success)
                {
                    await this.GetProducts();
                    _newProductId = result.Id ?? Guid.Empty;
                    _addStep2 = true;
                    _newVariantsError = null;
                    _newVariantForm.ProductId = _newProductId;
                }

                this.ShowToast(result, "Products", successFallback: "Product created successfully.", failureFallback: "We couldn't create that product. Try again.");
            });
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

            await _variantGate.RunAsync(async () =>
            {
                var response = await this.ProductVariantService.AddAsync(_editProduct.Id, _variantForm);
                this.ShowToast(response, "Product Variants", successFallback: "Variant added successfully.", failureFallback: "We couldn't add that variant. Try again.");
                if (response.Success)
                {
                    await LoadVariantsAsync(_editProduct.Id);
                }
            });
        }

        private async Task AddVariantForNewAsync()
        {
            if (_newProductId == Guid.Empty)
            {
                this.ToastService.ShowToast(ToastLevel.Error, "Save product first.", "Variant");
                return;
            }

            await _variantGate.RunAsync(async () =>
            {
                var response = await this.ProductVariantService.AddAsync(_newProductId, _newVariantForm);
                this.ShowToast(response, "Product Variants", successFallback: "Variant added successfully.", failureFallback: "We couldn't add that variant. Try again.");
                if (response.Success)
                {
                    if (!await LoadNewVariantsAsync())
                    {
                        return;
                    }

                    _newVariantForm = new CreateOrUpdateProductVariant { ProductId = _newProductId, SizeScale = 1, SizeValue = GetSizeOptions(1).FirstOrDefault() ?? "XS", Stock = 0 };
                }
            });
        }

        private async Task<bool> LoadNewVariantsAsync()
        {
            var variantsResult = await this.ProductVariantService.GetByProductIdAsync(_newProductId);
            if (this.QueryFailureNotifier.TryNotifyFailure(variantsResult, "Variants", showToast: false))
            {
                _newVariants = [];
                _newVariantsError = FeedbackMessageResolver.ResolveQueryFailure(variantsResult, "We couldn't reload product variants right now. Please try again.");
                return false;
            }

            _newVariantsError = null;
            _newVariants = (variantsResult.Data ?? []).ToList();
            return true;
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

            await _variantGate.RunAsync(async () =>
            {
                var response = await this.ProductVariantService.UpdateAsync(_variantForm);
                this.ShowToast(response, "Product Variants", successFallback: "Variant updated successfully.", failureFallback: "We couldn't update that variant. Try again.");
                if (response.Success)
                {
                    await LoadVariantsAsync(_editProduct.Id);
                }
            });
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
            await _variantGate.RunAsync(async () =>
            {
                var response = await this.ProductVariantService.DeleteAsync(id);
                this.ShowToast(response, "Product Variants", successFallback: "Variant deleted successfully.", failureFallback: "We couldn't delete that variant. Try again.");
                if (response.Success)
                {
                    await LoadVariantsAsync(_editProduct.Id);
                }
            });
        }

        private async Task OnFileSelected(InputFileChangeEventArgs e)
        {
            if (_isUploadingImage)
            {
                return;
            }

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

            _isUploadingImage = true;

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
                        message: "We couldn't upload the image. Try again.",
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
            finally
            {
                _isUploadingImage = false;
            }
        }

        private async Task OnEditFileSelected(InputFileChangeEventArgs e)
        {
            if (_isUploadingImage)
            {
                return;
            }

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

            _isUploadingImage = true;

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
                        message: "We couldn't upload the image. Try again.",
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
            finally
            {
                _isUploadingImage = false;
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

        private void ShowToast(ServiceResponse result, string heading, string successFallback = "Saved successfully.", string failureFallback = "Request failed.")
        {
            var level = result.Success ? ToastLevel.Success : ToastLevel.Error;
            var icon = result.Success ? ToastIcon.Success : ToastIcon.Error;

            this.ToastService.ShowToast(level: level, message: FeedbackMessageResolver.ResolveMutation(result, successFallback, failureFallback), heading: heading, iconClass: icon);
        }

        private void ShowToast<TPayload>(ServiceResponse<TPayload> result, string heading, string successFallback = "Saved successfully.", string failureFallback = "Request failed.")
        {
            var level = result.Success ? ToastLevel.Success : ToastLevel.Error;
            var icon = result.Success ? ToastIcon.Success : ToastIcon.Error;
            var message = FeedbackMessageResolver.ResolveMutation(result, successFallback, failureFallback);

            this.ToastService.ShowToast(level: level, message: message, heading: heading, iconClass: icon);
        }

        private async Task SaveProductSeoAsync()
        {
            if (_isProductSeoSaving || _editProduct.Id == Guid.Empty)
            {
                return;
            }

            _isProductSeoSaving = true;

            try
            {
                var result = await this.ProductSeoService.UpdateAsync(_editProduct.Id, _editProductSeo);
                if (result.Success)
                {
                    _productSeoError = null;

                    if (result.Payload is not null)
                    {
                        _editProductSeo = MapProductSeo(result.Payload);
                    }
                }

                this.ShowToast(result, "Product SEO");
            }
            finally
            {
                _isProductSeoSaving = false;
            }
        }

        private async Task UpdateProductAsync()
        {
            if (IsUpdatingProduct)
            {
                return;
            }

            if (string.IsNullOrEmpty(_editProduct.Name))
            {
                this.ShowToast(new ServiceResponse(false, "Product name is required."), "Products");
                return;
            }

            if (string.IsNullOrEmpty(_editProduct.Image))
            {
                this.ShowToast(new ServiceResponse(false, "Please upload an image for the product."), "Products");
                return;
            }

            await _updateProductGate.RunAsync(async () =>
            {
                var result = await this.ProductService.UpdateAsync(_editProduct);
                if (result.Success)
                {
                    _showEditDialog = false;
                    await this.GetProducts();
                }

                this.ShowToast(result, "Products", successFallback: "Product updated successfully.", failureFallback: "We couldn't update that product. Try again.");
            });
        }

        private void CancelDelete()
        {
            _showConfirmDeleteDialog = false;
            _productToDelete = Guid.Empty;
        }

        private static UpdateProductSeo CreateDefaultProductSeo(Guid productId)
        {
            return new UpdateProductSeo
            {
                ProductId = productId,
                RobotsIndex = true,
                RobotsFollow = true,
                IsPublished = true,
            };
        }

        private static UpdateProductSeo MapProductSeo(GetProductSeo seo)
        {
            return new UpdateProductSeo
            {
                ProductId = seo.ProductId,
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
                PublishedOn = seo.PublishedOn,
            };
        }

        private enum ProductEditorTab
        {
            Catalog,
            Seo,
        }
    }
}