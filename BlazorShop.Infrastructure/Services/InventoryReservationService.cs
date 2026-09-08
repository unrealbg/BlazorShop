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
        private readonly IDbContextFactory<AppDbContext> _contextFactory;

        public InventoryReservationService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public InventoryReservationService(AppDbContext db)
            : this(CreateFactory(db))
        {
        }

        public Task<InventoryReservationResult> CreateOrderWithInventoryAsync(
            Order order,
            InventoryReservationStatus reservationStatus,
            CancellationToken cancellationToken = default) =>
            CreateOrderWithInventoryAsync(order, reservationStatus, null, cancellationToken);

        public Task<InventoryReservationResult> CreateOrderWithInventoryAsync(
            Order order,
            InventoryReservationStatus reservationStatus,
            Guid checkoutIdempotencyRecordId,
            CancellationToken cancellationToken = default) =>
            CreateOrderWithInventoryAsync(order, reservationStatus, (Guid?)checkoutIdempotencyRecordId, cancellationToken);

        private async Task<InventoryReservationResult> CreateOrderWithInventoryAsync(
            Order order,
            InventoryReservationStatus reservationStatus,
            Guid? checkoutIdempotencyRecordId,
            CancellationToken cancellationToken)
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
            var commitMayHaveSucceeded = false;
            await using var strategyContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var executionStrategy = strategyContext.Database.CreateExecutionStrategy();

            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
                if (commitMayHaveSucceeded)
                {
                    var verification = await VerifyCreatedOrderAsync(
                        db,
                        order,
                        targets,
                        reservationStatus,
                        cancellationToken);
                    if (verification == CommitVerification.Succeeded)
                    {
                        return new InventoryReservationResult(true);
                    }

                    if (verification == CommitVerification.Conflict)
                    {
                        return new InventoryReservationResult(
                            false,
                            "The order exists, but its inventory reservation could not be verified.");
                    }

                    commitMayHaveSucceeded = false;
                }

                await using var transaction = await db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

                if (checkoutIdempotencyRecordId.HasValue)
                {
                    var idempotencyRecord = (await db.CheckoutIdempotencyRecords
                        .FromSqlInterpolated(
                            $"SELECT * FROM \"CheckoutIdempotencyRecords\" WHERE \"Id\" = {checkoutIdempotencyRecordId.Value} FOR UPDATE")
                        .ToListAsync(cancellationToken))
                        .SingleOrDefault();
                    if (idempotencyRecord is null
                        || idempotencyRecord.OrderId != order.Id
                        || !string.Equals(idempotencyRecord.UserId, order.UserId, StringComparison.Ordinal)
                        || !string.Equals(idempotencyRecord.OrderReference, order.Reference, StringComparison.Ordinal))
                    {
                        return await RollBackAsync(
                            db,
                            transaction,
                            new InventoryReservationResult(
                                false,
                                "The checkout idempotency claim does not match the requested order."),
                            cancellationToken);
                    }
                }

                var existingOrder = (await db.Orders
                    .FromSqlInterpolated($"SELECT * FROM \"Orders\" WHERE \"Id\" = {order.Id} FOR UPDATE")
                    .ToListAsync(cancellationToken))
                    .SingleOrDefault();
                if (existingOrder is not null)
                {
                    var verification = await VerifyCreatedOrderAsync(
                        db,
                        order,
                        targets,
                        reservationStatus,
                        cancellationToken);
                    return await RollBackAsync(
                        db,
                        transaction,
                        verification == CommitVerification.Succeeded
                            ? new InventoryReservationResult(true)
                            : new InventoryReservationResult(
                                false,
                                "The order exists, but its inventory reservation could not be verified."),
                        cancellationToken);
                }

                var products = new Dictionary<Guid, Product>();
                foreach (var productId in targets.Select(target => target.ProductId).Distinct().Order())
                {
                    var product = (await db.Products
                        .FromSqlInterpolated($"SELECT * FROM \"Products\" WHERE \"Id\" = {productId} FOR UPDATE")
                        .ToListAsync(cancellationToken))
                        .SingleOrDefault();

                    if (product is null)
                    {
                        return await RollBackAsync(
                            db,
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
                    var variant = (await db.ProductVariants
                        .FromSqlInterpolated($"SELECT * FROM \"ProductVariants\" WHERE \"Id\" = {variantId} FOR UPDATE")
                        .ToListAsync(cancellationToken))
                        .SingleOrDefault();

                    if (variant is null)
                    {
                        return await RollBackAsync(
                            db,
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
                    : (await db.ProductVariants
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
                                db,
                                transaction,
                                new InventoryReservationResult(false, "A selected product variant does not belong to the requested product."),
                                cancellationToken);
                        }

                        if (variant.Stock < target.Quantity)
                        {
                            return await RollBackAsync(
                                db,
                                transaction,
                                new InventoryReservationResult(false, "The requested quantity exceeds the selected product variant's current availability."),
                                cancellationToken);
                        }

                        continue;
                    }

                    if (productIdsWithVariants.Contains(target.ProductId))
                    {
                        return await RollBackAsync(
                            db,
                            transaction,
                            new InventoryReservationResult(false, "A product variant must be selected for this product."),
                            cancellationToken);
                    }

                    if (products[target.ProductId].Quantity < target.Quantity)
                    {
                        return await RollBackAsync(
                            db,
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

                    db.InventoryReservations.Add(new InventoryReservation
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

                db.Orders.Add(order);
                await db.SaveChangesAsync(cancellationToken);

                commitMayHaveSucceeded = true;
                await transaction.CommitAsync(cancellationToken);
                commitMayHaveSucceeded = false;
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

            var commitMayHaveSucceeded = false;
            await using var strategyContext = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var executionStrategy = strategyContext.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
                if (commitMayHaveSucceeded)
                {
                    if (await VerifyTransitionAsync(
                        db,
                        orderId,
                        orderStatus,
                        reservationStatus,
                        cancellationToken))
                    {
                        return new InventoryTransitionResult(InventoryTransitionOutcome.Applied);
                    }

                    commitMayHaveSucceeded = false;
                }

                await using var transaction = await db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    cancellationToken);

                var order = (await db.Orders
                    .FromSqlInterpolated($"SELECT * FROM \"Orders\" WHERE \"Id\" = {orderId} FOR UPDATE")
                    .ToListAsync(cancellationToken))
                    .SingleOrDefault();
                if (order is null)
                {
                    return await RollBackAsync(
                        db,
                        transaction,
                        new InventoryTransitionResult(InventoryTransitionOutcome.OrderNotFound),
                        cancellationToken);
                }

                var result = await InventoryReservationTransitionOperation.ApplyAsync(
                    db,
                    order,
                    orderStatus,
                    reservationStatus,
                    cancellationToken);
                if (result.Outcome is InventoryTransitionOutcome.AlreadyApplied
                    or InventoryTransitionOutcome.InvalidTransition)
                {
                    return await RollBackAsync(db, transaction, result, cancellationToken);
                }

                await db.SaveChangesAsync(cancellationToken);
                commitMayHaveSucceeded = true;
                await transaction.CommitAsync(cancellationToken);
                commitMayHaveSucceeded = false;
                return result;
            });
        }

        private static async Task<CommitVerification> VerifyCreatedOrderAsync(
            AppDbContext db,
            Order requestedOrder,
            IReadOnlyList<InventoryTarget> targets,
            InventoryReservationStatus reservationStatus,
            CancellationToken cancellationToken)
        {
            var persistedOrder = await db.Orders
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == requestedOrder.Id, cancellationToken);
            if (persistedOrder is null)
            {
                return CommitVerification.NotFound;
            }

            var reservations = await db.InventoryReservations
                .AsNoTracking()
                .Where(item => item.OrderId == requestedOrder.Id)
                .ToListAsync(cancellationToken);
            if (!string.Equals(persistedOrder.Status, requestedOrder.Status, StringComparison.Ordinal)
                || !string.Equals(persistedOrder.UserId, requestedOrder.UserId, StringComparison.Ordinal)
                || !string.Equals(persistedOrder.Reference, requestedOrder.Reference, StringComparison.Ordinal)
                || reservations.Count != targets.Count)
            {
                return CommitVerification.Conflict;
            }

            foreach (var target in targets)
            {
                var reservation = reservations.SingleOrDefault(item =>
                    item.ProductId == target.ProductId
                    && item.ProductVariantId == target.ProductVariantId);
                if (reservation is null
                    || reservation.Quantity != target.Quantity
                    || reservation.Status != reservationStatus)
                {
                    return CommitVerification.Conflict;
                }
            }

            return CommitVerification.Succeeded;
        }

        private static async Task<bool> VerifyTransitionAsync(
            AppDbContext db,
            Guid orderId,
            string orderStatus,
            InventoryReservationStatus reservationStatus,
            CancellationToken cancellationToken)
        {
            var order = await db.Orders
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == orderId, cancellationToken);
            if (order is null || !string.Equals(order.Status, orderStatus, StringComparison.Ordinal))
            {
                return false;
            }

            var reservationStates = await db.InventoryReservations
                .AsNoTracking()
                .Where(item => item.OrderId == orderId)
                .Select(item => item.Status)
                .ToListAsync(cancellationToken);
            return reservationStates.Count == 0
                || reservationStates.All(status => status == reservationStatus);
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

        private static async Task<T> RollBackAsync<T>(
            AppDbContext db,
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
            T result,
            CancellationToken cancellationToken)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            return result;
        }

        private static IDbContextFactory<AppDbContext> CreateFactory(AppDbContext db)
        {
            var connectionString = db.Database.GetConnectionString()
                ?? throw new InvalidOperationException("A PostgreSQL connection string is required.");
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure();
                })
                .Options;
            return new OptionsAppDbContextFactory(options);
        }

        private sealed class OptionsAppDbContextFactory : IDbContextFactory<AppDbContext>
        {
            private readonly DbContextOptions<AppDbContext> _options;

            public OptionsAppDbContextFactory(DbContextOptions<AppDbContext> options)
            {
                _options = options;
            }

            public AppDbContext CreateDbContext() => new(_options);

            public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(CreateDbContext());
        }

        private enum CommitVerification
        {
            NotFound,
            Succeeded,
            Conflict,
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
