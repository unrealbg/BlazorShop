namespace BlazorShop.Infrastructure.Services
{
    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    internal static class InventoryReservationTransitionOperation
    {
        public static async Task<InventoryTransitionResult> ApplyAsync(
            AppDbContext db,
            Order order,
            string orderStatus,
            InventoryReservationStatus reservationStatus,
            CancellationToken cancellationToken)
        {
            var reservations = await db.InventoryReservations
                .FromSqlInterpolated($"SELECT * FROM \"InventoryReservations\" WHERE \"OrderId\" = {order.Id} ORDER BY \"Id\" FOR UPDATE")
                .ToListAsync(cancellationToken);

            if (reservations.Count == 0)
            {
                if (string.Equals(order.Status, orderStatus, StringComparison.Ordinal)
                    || (string.Equals(order.Status, PaymentOrderStatus.Paid, StringComparison.Ordinal)
                        && reservationStatus == InventoryReservationStatus.Released))
                {
                    return new InventoryTransitionResult(InventoryTransitionOutcome.AlreadyApplied);
                }

                order.Status = orderStatus;
                return new InventoryTransitionResult(InventoryTransitionOutcome.Applied);
            }

            if (reservationStatus == InventoryReservationStatus.Consumed)
            {
                if (reservations.Any(reservation => reservation.Status == InventoryReservationStatus.Released))
                {
                    return new InventoryTransitionResult(
                        InventoryTransitionOutcome.InvalidTransition,
                        "Released inventory cannot be consumed.");
                }

                if (reservations.All(reservation => reservation.Status == InventoryReservationStatus.Consumed)
                    && string.Equals(order.Status, orderStatus, StringComparison.Ordinal))
                {
                    return new InventoryTransitionResult(InventoryTransitionOutcome.AlreadyApplied);
                }

                var consumedOn = DateTime.UtcNow;
                foreach (var reservation in reservations.Where(item => item.Status == InventoryReservationStatus.Reserved))
                {
                    reservation.Status = InventoryReservationStatus.Consumed;
                    reservation.ConsumedOn = consumedOn;
                }

                order.Status = orderStatus;
                return new InventoryTransitionResult(InventoryTransitionOutcome.Applied);
            }

            if (string.Equals(order.Status, PaymentOrderStatus.Paid, StringComparison.Ordinal)
                || reservations.Any(reservation => reservation.Status == InventoryReservationStatus.Consumed))
            {
                return new InventoryTransitionResult(InventoryTransitionOutcome.AlreadyApplied);
            }

            if (reservations.All(reservation => reservation.Status == InventoryReservationStatus.Released))
            {
                return new InventoryTransitionResult(InventoryTransitionOutcome.AlreadyApplied);
            }

            var reservedItems = reservations
                .Where(reservation => reservation.Status == InventoryReservationStatus.Reserved)
                .ToArray();
            var products = new Dictionary<Guid, Product>();
            foreach (var productId in reservedItems.Select(item => item.ProductId).Distinct().Order())
            {
                var product = (await db.Products
                    .FromSqlInterpolated($"SELECT * FROM \"Products\" WHERE \"Id\" = {productId} FOR UPDATE")
                    .ToListAsync(cancellationToken))
                    .SingleOrDefault();
                if (product is not null)
                {
                    products.Add(productId, product);
                }
            }

            var variants = new Dictionary<Guid, ProductVariant>();
            foreach (var variantId in reservedItems
                .Where(item => item.ProductVariantId.HasValue)
                .Select(item => item.ProductVariantId!.Value)
                .Distinct()
                .Order())
            {
                var variant = (await db.ProductVariants
                    .FromSqlInterpolated($"SELECT * FROM \"ProductVariants\" WHERE \"Id\" = {variantId} FOR UPDATE")
                    .ToListAsync(cancellationToken))
                    .SingleOrDefault();
                if (variant is not null)
                {
                    variants.Add(variantId, variant);
                }
            }

            var productOnlyIds = reservedItems
                .Where(item => !item.ProductVariantId.HasValue && products.ContainsKey(item.ProductId))
                .Select(item => item.ProductId)
                .Distinct()
                .ToArray();
            var productIdsWithVariants = productOnlyIds.Length == 0
                ? new HashSet<Guid>()
                : (await db.ProductVariants
                    .AsNoTracking()
                    .Where(variant => productOnlyIds.Contains(variant.ProductId))
                    .Select(variant => variant.ProductId)
                    .Distinct()
                    .ToListAsync(cancellationToken))
                    .ToHashSet();

            var restorationSkipped = false;
            var releasedOn = DateTime.UtcNow;
            foreach (var reservation in reservedItems)
            {
                if (reservation.ProductVariantId.HasValue
                    && variants.TryGetValue(reservation.ProductVariantId.Value, out var variant)
                    && variant.ProductId == reservation.ProductId)
                {
                    variant.Stock = checked(variant.Stock + reservation.Quantity);
                }
                else if (!reservation.ProductVariantId.HasValue
                    && products.TryGetValue(reservation.ProductId, out var product)
                    && !productIdsWithVariants.Contains(reservation.ProductId))
                {
                    product.Quantity = checked(product.Quantity + reservation.Quantity);
                }
                else
                {
                    restorationSkipped = true;
                }

                reservation.Status = InventoryReservationStatus.Released;
                reservation.ReleasedOn = releasedOn;
            }

            order.Status = orderStatus;
            return new InventoryTransitionResult(
                InventoryTransitionOutcome.Applied,
                restorationSkipped
                    ? "One or more deleted inventory targets could not be restored; their reservations were released terminally."
                    : null);
        }
    }
}
