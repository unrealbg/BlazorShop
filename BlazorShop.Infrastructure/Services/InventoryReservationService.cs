namespace BlazorShop.Infrastructure.Services
{
    using System.Data;

    using BlazorShop.Domain.Contracts.Payment;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    public sealed class InventoryReservationService : IInventoryReservationService
    {
        private readonly AppDbContext _db;

        public InventoryReservationService(AppDbContext db)
        {
            _db = db;
        }

        public async Task<InventoryReservationResult> CreateOrderWithInventoryAsync(
            Order order,
            InventoryReservationStatus reservationStatus,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(order);

            if (reservationStatus is not (InventoryReservationStatus.Reserved or InventoryReservationStatus.Consumed))
            {
                throw new ArgumentOutOfRangeException(nameof(reservationStatus));
            }

            var normalization = NormalizeTargets(order.Lines);
            if (!normalization.Success)
            {
                return new InventoryReservationResult(false, normalization.ErrorMessage);
            }

            var targets = normalization.Targets;
            var executionStrategy = _db.Database.CreateExecutionStrategy();

            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

                var products = new Dictionary<Guid, Product>();
                foreach (var productId in targets.Select(target => target.ProductId).Distinct().Order())
                {
                    var lockedProducts = await _db.Products
                        .FromSqlInterpolated($"SELECT * FROM \"Products\" WHERE \"Id\" = {productId} FOR UPDATE")
                        .ToListAsync(cancellationToken);
                    var product = lockedProducts.SingleOrDefault();

                    if (product is null)
                    {
                        return await RollBackAsync(
                            transaction,
                            new InventoryReservationResult(false, "A product in the cart no longer exists."),
                            cancellationToken);
                    }

                    products.Add(productId, product);
                }

                var variants = new Dictionary<Guid, ProductVariant>();
                foreach (var variantId in targets
                    .Where(target => target.ProductVariantId.HasValue)
                    .Select(target => target.ProductVariantId!.Value)
                    .Distinct()
                    .Order())
                {
                    var lockedVariants = await _db.ProductVariants
                        .FromSqlInterpolated($"SELECT * FROM \"ProductVariants\" WHERE \"Id\" = {variantId} FOR UPDATE")
                        .ToListAsync(cancellationToken);
                    var variant = lockedVariants.SingleOrDefault();

                    if (variant is null)
                    {
                        return await RollBackAsync(
                            transaction,
                            new InventoryReservationResult(false, "A selected product variant no longer exists."),
                            cancellationToken);
                    }

                    variants.Add(variantId, variant);
                }

                var productOnlyIds = targets
                    .Where(target => !target.ProductVariantId.HasValue)
                    .Select(target => target.ProductId)
                    .Distinct()
                    .ToArray();
                var productIdsWithVariants = productOnlyIds.Length == 0
                    ? new HashSet<Guid>()
                    : (await _db.ProductVariants
                        .AsNoTracking()
                        .Where(variant => productOnlyIds.Contains(variant.ProductId))
                        .Select(variant => variant.ProductId)
                        .Distinct()
                        .ToListAsync(cancellationToken))
                        .ToHashSet();

                foreach (var target in targets)
                {
                    if (target.ProductVariantId.HasValue)
                    {
                        var variant = variants[target.ProductVariantId.Value];
                        if (variant.ProductId != target.ProductId)
                        {
                            return await RollBackAsync(
                                transaction,
                                new InventoryReservationResult(false, "A selected product variant does not belong to the requested product."),
                                cancellationToken);
                        }

                        if (variant.Stock < target.Quantity)
                        {
                            return await RollBackAsync(
                                transaction,
                                new InventoryReservationResult(false, "The requested quantity exceeds the selected product variant's current availability."),
                                cancellationToken);
                        }

                        continue;
                    }

                    if (productIdsWithVariants.Contains(target.ProductId))
                    {
                        return await RollBackAsync(
                            transaction,
                            new InventoryReservationResult(false, "A product variant must be selected for this product."),
                            cancellationToken);
                    }

                    if (products[target.ProductId].Quantity < target.Quantity)
                    {
                        return await RollBackAsync(
                            transaction,
                            new InventoryReservationResult(false, "The requested quantity exceeds the product's current availability."),
                            cancellationToken);
                    }
                }

                var now = DateTime.UtcNow;
                foreach (var target in targets)
                {
                    if (target.ProductVariantId.HasValue)
                    {
                        variants[target.ProductVariantId.Value].Stock -= target.Quantity;
                    }
                    else
                    {
                        products[target.ProductId].Quantity -= target.Quantity;
                    }

                    _db.InventoryReservations.Add(new InventoryReservation
                    {
                        OrderId = order.Id,
                        ProductId = target.ProductId,
                        ProductVariantId = target.ProductVariantId,
                        Quantity = target.Quantity,
                        Status = reservationStatus,
                        CreatedOn = now,
                        ConsumedOn = reservationStatus == InventoryReservationStatus.Consumed ? now : null,
                    });
                }

                _db.Orders.Add(order);
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new InventoryReservationResult(true);
            });
        }

        public async Task<InventoryTransitionResult> TransitionOrderAsync(
            Guid orderId,
            string orderStatus,
            InventoryReservationStatus reservationStatus,
            CancellationToken cancellationToken = default)
        {
            if (orderId == Guid.Empty)
            {
                return new InventoryTransitionResult(InventoryTransitionOutcome.OrderNotFound);
            }

            if (reservationStatus is not (InventoryReservationStatus.Consumed or InventoryReservationStatus.Released))
            {
                throw new ArgumentOutOfRangeException(nameof(reservationStatus));
            }

            var executionStrategy = _db.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

                var lockedOrders = await _db.Orders
                    .FromSqlInterpolated($"SELECT * FROM \"Orders\" WHERE \"Id\" = {orderId} FOR UPDATE")
                    .ToListAsync(cancellationToken);
                var order = lockedOrders.SingleOrDefault();
                if (order is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();
                    return new InventoryTransitionResult(InventoryTransitionOutcome.OrderNotFound);
                }

                var reservations = await _db.InventoryReservations
                    .FromSqlInterpolated($"SELECT * FROM \"InventoryReservations\" WHERE \"OrderId\" = {orderId} ORDER BY \"Id\" FOR UPDATE")
                    .ToListAsync(cancellationToken);

                if (reservations.Count == 0)
                {
                    if (string.Equals(order.Status, orderStatus, StringComparison.Ordinal)
                        || (string.Equals(order.Status, PaymentOrderStatus.Paid, StringComparison.Ordinal)
                            && reservationStatus == InventoryReservationStatus.Released))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _db.ChangeTracker.Clear();
                        return new InventoryTransitionResult(InventoryTransitionOutcome.AlreadyApplied);
                    }

                    order.Status = orderStatus;
                    await _db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return new InventoryTransitionResult(InventoryTransitionOutcome.Applied);
                }

                if (reservationStatus == InventoryReservationStatus.Consumed)
                {
                    if (reservations.Any(reservation => reservation.Status == InventoryReservationStatus.Released))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _db.ChangeTracker.Clear();
                        return new InventoryTransitionResult(
                            InventoryTransitionOutcome.InvalidTransition,
                            "Released inventory cannot be consumed.");
                    }

                    if (reservations.All(reservation => reservation.Status == InventoryReservationStatus.Consumed)
                        && string.Equals(order.Status, orderStatus, StringComparison.Ordinal))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _db.ChangeTracker.Clear();
                        return new InventoryTransitionResult(InventoryTransitionOutcome.AlreadyApplied);
                    }

                    var consumedOn = DateTime.UtcNow;
                    foreach (var reservation in reservations.Where(item => item.Status == InventoryReservationStatus.Reserved))
                    {
                        reservation.Status = InventoryReservationStatus.Consumed;
                        reservation.ConsumedOn = consumedOn;
                    }

                    order.Status = orderStatus;
                    await _db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return new InventoryTransitionResult(InventoryTransitionOutcome.Applied);
                }

                if (string.Equals(order.Status, PaymentOrderStatus.Paid, StringComparison.Ordinal)
                    || reservations.Any(reservation => reservation.Status == InventoryReservationStatus.Consumed))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();
                    return new InventoryTransitionResult(InventoryTransitionOutcome.AlreadyApplied);
                }

                if (reservations.All(reservation => reservation.Status == InventoryReservationStatus.Released))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _db.ChangeTracker.Clear();
                    return new InventoryTransitionResult(InventoryTransitionOutcome.AlreadyApplied);
                }

                var reservedItems = reservations
                    .Where(reservation => reservation.Status == InventoryReservationStatus.Reserved)
                    .ToArray();
                var products = new Dictionary<Guid, Product>();
                foreach (var productId in reservedItems.Select(item => item.ProductId).Distinct().Order())
                {
                    var lockedProducts = await _db.Products
                        .FromSqlInterpolated($"SELECT * FROM \"Products\" WHERE \"Id\" = {productId} FOR UPDATE")
                        .ToListAsync(cancellationToken);
                    var product = lockedProducts.SingleOrDefault();
                    if (product is null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _db.ChangeTracker.Clear();
                        return new InventoryTransitionResult(
                            InventoryTransitionOutcome.InvalidTransition,
                            "Reserved product inventory no longer exists.");
                    }

                    products.Add(productId, product);
                }

                var variants = new Dictionary<Guid, ProductVariant>();
                foreach (var variantId in reservedItems
                    .Where(item => item.ProductVariantId.HasValue)
                    .Select(item => item.ProductVariantId!.Value)
                    .Distinct()
                    .Order())
                {
                    var lockedVariants = await _db.ProductVariants
                        .FromSqlInterpolated($"SELECT * FROM \"ProductVariants\" WHERE \"Id\" = {variantId} FOR UPDATE")
                        .ToListAsync(cancellationToken);
                    var variant = lockedVariants.SingleOrDefault();
                    if (variant is null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        _db.ChangeTracker.Clear();
                        return new InventoryTransitionResult(
                            InventoryTransitionOutcome.InvalidTransition,
                            "Reserved product variant inventory no longer exists.");
                    }

                    variants.Add(variantId, variant);
                }

                var releasedOn = DateTime.UtcNow;
                foreach (var reservation in reservedItems)
                {
                    if (reservation.ProductVariantId.HasValue)
                    {
                        variants[reservation.ProductVariantId.Value].Stock = checked(
                            variants[reservation.ProductVariantId.Value].Stock + reservation.Quantity);
                    }
                    else
                    {
                        products[reservation.ProductId].Quantity = checked(
                            products[reservation.ProductId].Quantity + reservation.Quantity);
                    }

                    reservation.Status = InventoryReservationStatus.Released;
                    reservation.ReleasedOn = releasedOn;
                }

                order.Status = orderStatus;
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new InventoryTransitionResult(InventoryTransitionOutcome.Applied);
            });
        }

        private static TargetNormalizationResult NormalizeTargets(IEnumerable<OrderLine> lines)
        {
            var quantities = new Dictionary<(Guid ProductId, Guid? ProductVariantId), long>();
            foreach (var line in lines)
            {
                if (line.ProductId == Guid.Empty || line.Quantity <= 0)
                {
                    return TargetNormalizationResult.Failure("Every order line must have a valid product and quantity.");
                }

                var key = (line.ProductId, line.ProductVariantId);
                quantities.TryGetValue(key, out var currentQuantity);
                var combinedQuantity = currentQuantity + line.Quantity;
                if (combinedQuantity > int.MaxValue)
                {
                    return TargetNormalizationResult.Failure("The requested inventory quantity is too large.");
                }

                quantities[key] = combinedQuantity;
            }

            if (quantities.Count == 0)
            {
                return TargetNormalizationResult.Failure("Your cart is empty.");
            }

            var targets = quantities
                .Select(item => new InventoryTarget(item.Key.ProductId, item.Key.ProductVariantId, (int)item.Value))
                .OrderBy(target => target.ProductId)
                .ThenBy(target => target.ProductVariantId)
                .ToArray();
            return TargetNormalizationResult.Successful(targets);
        }

        private async Task<T> RollBackAsync<T>(
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
            T result,
            CancellationToken cancellationToken)
        {
            await transaction.RollbackAsync(cancellationToken);
            _db.ChangeTracker.Clear();
            return result;
        }

        private sealed record InventoryTarget(Guid ProductId, Guid? ProductVariantId, int Quantity);

        private sealed record TargetNormalizationResult(
            bool Success,
            IReadOnlyList<InventoryTarget> Targets,
            string? ErrorMessage)
        {
            public static TargetNormalizationResult Successful(IReadOnlyList<InventoryTarget> targets) =>
                new(true, targets, null);

            public static TargetNormalizationResult Failure(string message) =>
                new(false, [], message);
        }
    }
}
