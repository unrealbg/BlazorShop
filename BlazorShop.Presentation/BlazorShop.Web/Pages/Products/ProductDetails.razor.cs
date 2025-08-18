namespace BlazorShop.Web.Pages.Products
{
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

        private async Task HandleAddToCart()
        {
            if (this.OnAddToCart.HasDelegate)
            {
                await this.OnAddToCart.InvokeAsync(this.Product.Id);
            }

            await CloseModalInternal();
        }

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