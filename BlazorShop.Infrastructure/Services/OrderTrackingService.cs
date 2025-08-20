namespace BlazorShop.Infrastructure.Services
{
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Infrastructure.Data;
    using Microsoft.EntityFrameworkCore;

    public class OrderTrackingService : IOrderTrackingService
    {
        private readonly AppDbContext _db;
        private readonly IEmailService _email;

        public OrderTrackingService(AppDbContext db, IEmailService email)
        {
            _db = db;
            _email = email;
        }

        public async Task UpdateTrackingAsync(Guid orderId, string carrier, string trackingNumber, string trackingUrl)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return;

            order.ShippingCarrier = carrier;
            order.TrackingNumber = trackingNumber;
            order.TrackingUrl = trackingUrl;
            order.LastTrackingUpdate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _ = NotifyAsync(order.UserId, "Tracking updated", $@"<p>Your order <b>{order.Reference}</b> tracking details were updated.</p>
<ul>
<li>Carrier: <b>{carrier}</b></li>
<li>Tracking Number: <b>{trackingNumber}</b></li>
<li>Tracking URL: <a href=""{trackingUrl}"">{trackingUrl}</a></li>
</ul>
<p>You can always check your order status in the My Orders page.</p>");
        }

        public async Task UpdateShippingStatusAsync(Guid orderId, string shippingStatus, DateTime? shippedOn = null, DateTime? deliveredOn = null)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return;

            order.ShippingStatus = shippingStatus;
            order.ShippedOn = shippedOn ?? order.ShippedOn;
            order.DeliveredOn = deliveredOn ?? order.DeliveredOn;
            order.LastTrackingUpdate = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _ = NotifyAsync(order.UserId, "Shipping status updated", $@"<p>Your order <b>{order.Reference}</b> shipping status changed to <b>{shippingStatus}</b>.</p>
<p>You can track your order in the My Orders page.</p>");
        }

        private async Task NotifyAsync(string? userId, string subject, string body)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userId)) return;
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null || string.IsNullOrWhiteSpace(user.Email)) return;
                await _email.SendEmailAsync(user.Email!, subject, body);
            }
            catch
            {
                // ignore notification failures
            }
        }
    }
}
