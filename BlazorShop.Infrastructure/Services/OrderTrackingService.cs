namespace BlazorShop.Infrastructure.Services
{
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;
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

        public async Task<bool> UpdateTrackingAsync(Guid orderId, string carrier, string trackingNumber, string trackingUrl)
        {
            return (await UpdateTrackingDetailsAsync(orderId, carrier, trackingNumber, trackingUrl)).Success;
        }

        public async Task<bool> UpdateShippingStatusAsync(
            Guid orderId,
            string shippingStatus,
            DateTime? shippedOn = null,
            DateTime? deliveredOn = null)
        {
            return (await TransitionFulfillmentAsync(orderId, shippingStatus, shippedOn, deliveredOn)).Success;
        }

        public async Task<OrderTrackingTransitionResult> UpdateTrackingDetailsAsync(
            Guid orderId,
            string carrier,
            string trackingNumber,
            string trackingUrl,
            CancellationToken cancellationToken = default)
        {
            var order = await LoadCurrentOrderAsync(orderId, cancellationToken);
            if (order is null)
            {
                return new(OrderTrackingTransitionOutcome.NotFound, "Order not found.");
            }

            if (string.Equals(order.ShippingCarrier, carrier, StringComparison.Ordinal)
                && string.Equals(order.TrackingNumber, trackingNumber, StringComparison.Ordinal)
                && string.Equals(order.TrackingUrl, trackingUrl, StringComparison.Ordinal))
            {
                return new(OrderTrackingTransitionOutcome.AlreadyApplied);
            }

            if (!OrderLifecyclePolicy.CanFulfill(order, out var reason))
            {
                return new(OrderTrackingTransitionOutcome.Conflict, reason);
            }

            order.ShippingCarrier = carrier;
            order.TrackingNumber = trackingNumber;
            order.TrackingUrl = trackingUrl;
            order.LastTrackingUpdate = DateTime.UtcNow;

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return new(OrderTrackingTransitionOutcome.Conflict, "The order changed while tracking was being updated.");
            }

            await NotifyAsync(
                order.CustomerEmailSnapshot,
                "Tracking updated",
                $@"<p>Your order <b>{order.Reference}</b> tracking details were updated.</p>
<ul>
<li>Carrier: <b>{carrier}</b></li>
<li>Tracking Number: <b>{trackingNumber}</b></li>
<li>Tracking URL: <a href=""{trackingUrl}"">{trackingUrl}</a></li>
</ul>");
            return new(OrderTrackingTransitionOutcome.Applied);
        }

        public async Task<OrderTrackingTransitionResult> TransitionFulfillmentAsync(
            Guid orderId,
            string fulfillmentStatus,
            DateTime? shippedOn = null,
            DateTime? deliveredOn = null,
            CancellationToken cancellationToken = default)
        {
            if (!Enum.TryParse<FulfillmentStatus>(fulfillmentStatus, true, out var target)
                || !Enum.IsDefined(target))
            {
                return new(OrderTrackingTransitionOutcome.ValidationError, "Fulfillment status is invalid.");
            }

            if ((shippedOn.HasValue && shippedOn.Value.Kind != DateTimeKind.Utc)
                || (deliveredOn.HasValue && deliveredOn.Value.Kind != DateTimeKind.Utc))
            {
                return new(OrderTrackingTransitionOutcome.ValidationError, "Fulfillment timestamps must be UTC.");
            }

            var now = DateTime.UtcNow;
            if ((shippedOn.HasValue && shippedOn.Value > now.AddMinutes(5))
                || (deliveredOn.HasValue && deliveredOn.Value > now.AddMinutes(5)))
            {
                return new(OrderTrackingTransitionOutcome.ValidationError, "Fulfillment timestamps cannot be in the future.");
            }

            var order = await LoadCurrentOrderAsync(orderId, cancellationToken);
            if (order is null)
            {
                return new(OrderTrackingTransitionOutcome.NotFound, "Order not found.");
            }

            if (order.FulfillmentStatus == target)
            {
                return new(OrderTrackingTransitionOutcome.AlreadyApplied);
            }

            if (!OrderLifecyclePolicy.CanFulfill(order, out var reason))
            {
                return new(OrderTrackingTransitionOutcome.Conflict, reason);
            }

            if (!OrderLifecyclePolicy.CanTransitionFulfillment(order, target, out _))
            {
                return new(
                    OrderTrackingTransitionOutcome.Conflict,
                    $"Fulfillment cannot transition from {order.FulfillmentStatus} to {target}.");
            }

            if (target == FulfillmentStatus.Shipped)
            {
                if (deliveredOn.HasValue)
                {
                    return new(OrderTrackingTransitionOutcome.ValidationError, "DeliveredOn is only valid for Delivered fulfillment.");
                }

                order.ShippedOn = shippedOn ?? now;
            }
            else if (shippedOn.HasValue)
            {
                return new(OrderTrackingTransitionOutcome.ValidationError, "ShippedOn can only be set by the Shipped transition.");
            }

            if (target == FulfillmentStatus.Delivered)
            {
                var effectiveDeliveredOn = deliveredOn ?? now;
                var hasLegacyUnknownShippedOn = OrderLifecyclePolicy.HasLegacyUnknownShippedOn(order);
                if ((!order.ShippedOn.HasValue && !hasLegacyUnknownShippedOn)
                    || (order.ShippedOn.HasValue && effectiveDeliveredOn < order.ShippedOn.Value))
                {
                    return new(OrderTrackingTransitionOutcome.ValidationError, "DeliveredOn cannot precede ShippedOn.");
                }

                order.DeliveredOn = effectiveDeliveredOn;
                if (order.PaymentStatus == OrderPaymentStatus.Paid)
                {
                    order.OrderStatus = OrderStatus.Completed;
                }
            }
            else if (deliveredOn.HasValue)
            {
                return new(OrderTrackingTransitionOutcome.ValidationError, "DeliveredOn is only valid for Delivered fulfillment.");
            }

            order.FulfillmentStatus = target;
            order.LastTrackingUpdate = now;

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.ChangeTracker.Clear();
                var current = await _db.Orders.AsNoTracking()
                    .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);
                return current?.FulfillmentStatus == target
                    ? new(OrderTrackingTransitionOutcome.AlreadyApplied)
                    : new(OrderTrackingTransitionOutcome.Conflict, "The order changed while fulfillment was being updated.");
            }

            await NotifyAsync(
                order.CustomerEmailSnapshot,
                "Shipping status updated",
                $"<p>Your order <b>{order.Reference}</b> fulfillment status changed to <b>{target}</b>.</p>");
            return new(OrderTrackingTransitionOutcome.Applied);
        }

        private async Task NotifyAsync(string? email, string subject, string body)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(email))
                {
                    await _email.SendEmailAsync(email, subject, body);
                }
            }
            catch
            {
                // Persisted order state is authoritative even when a best-effort email fails.
            }
        }

        private async Task<Order?> LoadCurrentOrderAsync(Guid orderId, CancellationToken cancellationToken)
        {
            var trackedEntry = _db.ChangeTracker.Entries<Order>()
                .SingleOrDefault(entry => entry.Entity.Id == orderId);
            if (trackedEntry is not null)
            {
                await trackedEntry.ReloadAsync(cancellationToken);
                return trackedEntry.State == EntityState.Detached ? null : trackedEntry.Entity;
            }

            return await _db.Orders.SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);
        }
    }
}
