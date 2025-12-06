namespace BlazorShop.Web.Pages.Products
{
    using BlazorShop.Web.Shared.Models.Payment;
    using BlazorShop.Web.Shared.Models.Product;

    using Microsoft.AspNetCore.Components;

    public partial class ProductDetails
    {
        [Parameter]
        public string Title { get; set; } = "Product Details";

        [Parameter]
        public GetProduct Product { get; set; } = new();

        [Parameter]
        public EventCallback OnClose { get; set; }

        [Parameter]
        public bool IsVisible { get; set; }

        [Parameter]
        public EventCallback<bool> IsVisibleChanged { get; set; }

        [Parameter]
        public EventCallback<Guid> OnAddToCart { get; set; }

        [Parameter]
        public EventCallback<ProcessCart> OnAddToCartVariant { get; set; }

        private Guid? _selectedVariantId;
        private decimal DisplayedPrice => Product?.Variants?.Any() == true
            ? (Product.Variants.FirstOrDefault(v => v.Id == _selectedVariantId)?.Price ?? Product.Price)
            : Product?.Price ?? 0m;

        private bool IsAddDisabled => Product?.Variants?.Any() == true && _selectedVariantId is null;

        private async Task HandleAddToCart()
        {
            if (Product?.Variants?.Any() == true)
            {
                if (_selectedVariantId is null)
                {
                    return;
                }

                var variant = Product.Variants.FirstOrDefault(x => x.Id == _selectedVariantId);
                if (variant is null)
                {
                    return;
                }

                if (OnAddToCartVariant.HasDelegate)
                {
                    var payload = new ProcessCart
                    {
                        ProductId = Product.Id,
                        VariantId = variant.Id,
                        SizeValue = variant.SizeValue,
                        UnitPrice = variant.Price ?? Product.Price,
                        Quantity = 1
                    };
                    await OnAddToCartVariant.InvokeAsync(payload);
                }
                else if (OnAddToCart.HasDelegate)
                {
                    await this.OnAddToCart.InvokeAsync(this.Product.Id);
                }
            }
            else if (this.OnAddToCart.HasDelegate && Product != null)
            {
                await this.OnAddToCart.InvokeAsync(this.Product.Id);
            }

            await CloseModalInternal();
        }

        private void OnVariantChanged(ChangeEventArgs e)
        {
            var value = e?.Value?.ToString();
            if (Guid.TryParse(value, out var id))
            {
                _selectedVariantId = id;
            }
            else
            {
                _selectedVariantId = null;
            }
            StateHasChanged();
        }

        private static string ScaleLabel(int scale) => scale switch
        {
            1 => "Clothing",
            2 => "Clothing EU",
            10 => "Shoes EU",
            11 => "Shoes US",
            12 => "Shoes UK",
            _ => "—"
        };

        private async Task CloseModalInternal()
        {
            IsVisible = false;
            if (IsVisibleChanged.HasDelegate)
            {
                await IsVisibleChanged.InvokeAsync(IsVisible);
            }
            await OnClose.InvokeAsync();
        }

        private async Task CloseModal()
        {
            await CloseModalInternal();
        }
    }
}