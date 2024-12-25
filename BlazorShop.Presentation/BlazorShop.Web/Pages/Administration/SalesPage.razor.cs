namespace BlazorShop.Web.Pages.Administration
{
    using BlazorShop.Web.Shared.Models.Payment;

    public partial class SalesPage
    {
        private IEnumerable<GetOrderItem> _orderItems = [];

        protected override async Task OnInitializedAsync()
        {
            this._orderItems = await this.CartService.GetOrderItemsAsync();
        }
    }
}