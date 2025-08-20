namespace BlazorShop.Web.Pages.Administration
{
    using BlazorShop.Web.Shared;
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Payment;

    public partial class SalesPage
    {
        private IEnumerable<GetOrderItem> _orderItems = [];

        private List<GetOrder> _orders = new();
        private List<GetOrder> _recentOrders = new();
        private decimal _totalRevenue;
        private decimal _todayRevenue;
        private int _countOrders;
        private int _pendingCount;
        private int _shippedCount;
        private int _inTransitCount;
        private int _ofdCount;
        private int _deliveredCount;

        protected override async Task OnInitializedAsync()
        {
            this._orderItems = await this.CartService.GetOrderItemsAsync();

            try
            {
                var client = await this.HttpClientHelper.GetPrivateClientAsync();
                var api = new ApiCall { Client = client, Route = Constant.Cart.GetAllOrders, Type = Constant.ApiCallType.Get };
                var http = await this.ApiCallHelper.ApiCallTypeCall<object>(api);
                _orders = http is null || !http.IsSuccessStatusCode
                    ? new()
                    : (await this.ApiCallHelper.GetServiceResponse<IEnumerable<GetOrder>>(http)).ToList();
            }
            catch
            {
                _orders = new();
            }

            ComputeStats();
        }

        private void ComputeStats()
        {
            _countOrders = _orders.Count;
            _totalRevenue = _orders.Sum(o => o.TotalAmount);
            var today = DateTime.UtcNow.Date;
            _todayRevenue = _orders.Where(o => o.CreatedOn.Date == today).Sum(o => o.TotalAmount);
            _pendingCount = _orders.Count(o => string.Equals(o.ShippingStatus, "PendingShipment", StringComparison.OrdinalIgnoreCase));
            _shippedCount = _orders.Count(o => string.Equals(o.ShippingStatus, "Shipped", StringComparison.OrdinalIgnoreCase));
            _inTransitCount = _orders.Count(o => string.Equals(o.ShippingStatus, "InTransit", StringComparison.OrdinalIgnoreCase));
            _ofdCount = _orders.Count(o => string.Equals(o.ShippingStatus, "OutForDelivery", StringComparison.OrdinalIgnoreCase));
            _deliveredCount = _orders.Count(o => string.Equals(o.ShippingStatus, "Delivered", StringComparison.OrdinalIgnoreCase));
            _recentOrders = _orders.OrderByDescending(o => o.CreatedOn).Take(10).ToList();
        }
    }
}