namespace BlazorShop.Infrastructure.Services
{
    using System.Text.Json;

    using BlazorShop.Application.DTOs.Payment;
    using BlazorShop.Application.Options;
    using BlazorShop.Application.Services.Contracts.Payment;
    using BlazorShop.Domain.Entities.Payment;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Options;

    using Npgsql;

    public sealed class CheckoutIdempotencyStore : ICheckoutIdempotencyStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly CheckoutIdempotencyOptions _options;

        public CheckoutIdempotencyStore(
            IDbContextFactory<AppDbContext> contextFactory,
            IOptions<CheckoutIdempotencyOptions> options)
        {
            _contextFactory = contextFactory;
            _options = options.Value;
        }

        public async Task<CheckoutIdempotencyClaim> ClaimAsync(
            string userId,
            Guid idempotencyKey,
            string requestFingerprint,
            Guid paymentMethodId,
            CancellationToken cancellationToken = default)
        {
            var ownerId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            await DeleteExpiredTerminalRecordsAsync(now, cancellationToken);
            var record = new CheckoutIdempotencyRecord
            {
                UserId = userId,
                IdempotencyKey = idempotencyKey,
                RequestFingerprint = requestFingerprint,
                PaymentMethodId = paymentMethodId,
                OrderId = Guid.NewGuid(),
                OrderReference = CreateOrderReference(paymentMethodId, now),
                LeaseOwnerId = ownerId,
                LeaseExpiresOn = now.AddSeconds(_options.LeaseSeconds),
                CreatedOn = now,
                UpdatedOn = now,
            };

            try
            {
                await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
                db.CheckoutIdempotencyRecords.Add(record);
                await db.SaveChangesAsync(cancellationToken);
                return new CheckoutIdempotencyClaim(
                    CheckoutIdempotencyClaimStatus.Acquired,
                    record,
                    ownerId);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                return await ObserveOrAcquireAsync(
                    userId,
                    idempotencyKey,
                    requestFingerprint,
                    ownerId,
                    cancellationToken);
            }
        }

        public async Task<bool> SetPendingOutcomeAsync(
            Guid recordId,
            Guid leaseOwnerId,
            PersistedCheckoutOutcome outcome,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.CheckoutIdempotencyRecords
                .Where(record => record.Id == recordId
                    && record.LeaseOwnerId == leaseOwnerId
                    && (record.State == CheckoutIdempotencyState.Processing
                        || record.State == CheckoutIdempotencyState.LocalCommitted))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(record => record.OutcomeJson, Serialize(outcome))
                        .SetProperty(record => record.OutcomeVersion, outcome.Version)
                        .SetProperty(record => record.UpdatedOn, now)
                        .SetProperty(record => record.LeaseExpiresOn, now.AddSeconds(_options.LeaseSeconds)),
                    cancellationToken) == 1;
        }

        public async Task<CheckoutProviderInitialization?> PrepareProviderInitializationAsync(
            Guid recordId,
            Guid leaseOwnerId,
            StripeCheckoutInitialization proposedInitialization,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var serializedInitialization = JsonSerializer.Serialize(
                proposedInitialization,
                SerializerOptions);
            await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
            var updated = await db.CheckoutIdempotencyRecords
                .Where(record => record.Id == recordId
                    && record.LeaseOwnerId == leaseOwnerId
                    && (record.State == CheckoutIdempotencyState.Processing
                        || record.State == CheckoutIdempotencyState.LocalCommitted))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(record => record.State, CheckoutIdempotencyState.LocalCommitted)
                        .SetProperty(
                            record => record.ProviderInitializationStartedOn,
                            record => record.ProviderInitializationStartedOn ?? now)
                        .SetProperty(
                            record => record.ProviderInitializationJson,
                            record => record.ProviderInitializationJson ?? serializedInitialization)
                        .SetProperty(record => record.UpdatedOn, now)
                        .SetProperty(record => record.LeaseExpiresOn, now.AddSeconds(_options.LeaseSeconds)),
                    cancellationToken);
            if (updated != 1)
            {
                return null;
            }

            var record = await db.CheckoutIdempotencyRecords
                .AsNoTracking()
                .SingleAsync(item => item.Id == recordId, cancellationToken);
            if (!record.ProviderInitializationStartedOn.HasValue
                || string.IsNullOrWhiteSpace(record.ProviderInitializationJson))
            {
                throw new InvalidOperationException(
                    "The checkout provider initialization snapshot was not persisted.");
            }

            var initialization = JsonSerializer.Deserialize<StripeCheckoutInitialization>(
                record.ProviderInitializationJson,
                SerializerOptions)
                ?? throw new InvalidOperationException(
                    "The checkout provider initialization snapshot is invalid.");
            return new CheckoutProviderInitialization(
                record.ProviderInitializationStartedOn.Value,
                initialization);
        }

        public async Task<bool> CompleteAsync(
            Guid recordId,
            Guid leaseOwnerId,
            PersistedCheckoutOutcome outcome,
            CheckoutIdempotencyState terminalState,
            CancellationToken cancellationToken = default)
        {
            if (terminalState is not (CheckoutIdempotencyState.Completed or CheckoutIdempotencyState.Failed))
            {
                throw new ArgumentOutOfRangeException(nameof(terminalState));
            }

            var now = DateTime.UtcNow;
            await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.CheckoutIdempotencyRecords
                .Where(record => record.Id == recordId
                    && record.LeaseOwnerId == leaseOwnerId
                    && (record.State == CheckoutIdempotencyState.Processing
                        || record.State == CheckoutIdempotencyState.LocalCommitted))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(record => record.State, terminalState)
                        .SetProperty(record => record.OutcomeJson, Serialize(outcome))
                        .SetProperty(record => record.OutcomeVersion, outcome.Version)
                        .SetProperty(record => record.UpdatedOn, now)
                        .SetProperty(record => record.CompletedOn, now)
                        .SetProperty(record => record.ExpiresOn, now.AddDays(_options.RetentionDays))
                        .SetProperty(record => record.LeaseOwnerId, (Guid?)null)
                        .SetProperty(record => record.LeaseExpiresOn, (DateTime?)null),
                    cancellationToken) == 1;
        }

        public async Task<bool> ReleaseLeaseAsync(
            Guid recordId,
            Guid leaseOwnerId,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.CheckoutIdempotencyRecords
                .Where(record => record.Id == recordId
                    && record.LeaseOwnerId == leaseOwnerId
                    && (record.State == CheckoutIdempotencyState.Processing
                        || record.State == CheckoutIdempotencyState.LocalCommitted))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(record => record.UpdatedOn, now)
                        .SetProperty(record => record.LeaseOwnerId, (Guid?)null)
                        .SetProperty(record => record.LeaseExpiresOn, now),
                    cancellationToken) == 1;
        }

        public async Task<CheckoutIdempotencyRecord?> GetAsync(
            Guid recordId,
            CancellationToken cancellationToken = default)
        {
            await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
            return await db.CheckoutIdempotencyRecords
                .AsNoTracking()
                .SingleOrDefaultAsync(record => record.Id == recordId, cancellationToken);
        }

        public PersistedCheckoutOutcome? ReadOutcome(CheckoutIdempotencyRecord record)
        {
            return string.IsNullOrWhiteSpace(record.OutcomeJson)
                ? null
                : JsonSerializer.Deserialize<PersistedCheckoutOutcome>(record.OutcomeJson, SerializerOptions);
        }

        private async Task<CheckoutIdempotencyClaim> ObserveOrAcquireAsync(
            string userId,
            Guid idempotencyKey,
            string requestFingerprint,
            Guid ownerId,
            CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(_options.DuplicateWaitMilliseconds);
            while (true)
            {
                await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
                var existing = await db.CheckoutIdempotencyRecords
                    .AsNoTracking()
                    .SingleAsync(
                        record => record.UserId == userId && record.IdempotencyKey == idempotencyKey,
                        cancellationToken);

                if (!string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
                {
                    return new CheckoutIdempotencyClaim(
                        CheckoutIdempotencyClaimStatus.Conflict,
                        existing,
                        ownerId);
                }

                if (existing.State is CheckoutIdempotencyState.Completed or CheckoutIdempotencyState.Failed)
                {
                    return new CheckoutIdempotencyClaim(
                        CheckoutIdempotencyClaimStatus.Replayed,
                        existing,
                        ownerId,
                        ReadOutcome(existing)
                            ?? throw new InvalidOperationException("A terminal checkout idempotency record has no outcome."));
                }

                var now = DateTime.UtcNow;
                if (!existing.LeaseExpiresOn.HasValue || existing.LeaseExpiresOn <= now)
                {
                    var acquired = await db.CheckoutIdempotencyRecords
                        .Where(record => record.Id == existing.Id
                            && (record.State == CheckoutIdempotencyState.Processing
                                || record.State == CheckoutIdempotencyState.LocalCommitted)
                            && (!record.LeaseExpiresOn.HasValue || record.LeaseExpiresOn <= now))
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(record => record.LeaseOwnerId, ownerId)
                                .SetProperty(record => record.LeaseExpiresOn, now.AddSeconds(_options.LeaseSeconds))
                                .SetProperty(record => record.UpdatedOn, now),
                            cancellationToken);
                    if (acquired == 1)
                    {
                        var acquiredRecord = await db.CheckoutIdempotencyRecords
                            .AsNoTracking()
                            .SingleAsync(
                                record => record.Id == existing.Id
                                    && record.LeaseOwnerId == ownerId,
                                cancellationToken);
                        return new CheckoutIdempotencyClaim(
                            CheckoutIdempotencyClaimStatus.Acquired,
                            acquiredRecord,
                            ownerId,
                            ReadOutcome(acquiredRecord));
                    }
                }

                if (DateTime.UtcNow >= deadline)
                {
                    return new CheckoutIdempotencyClaim(
                        CheckoutIdempotencyClaimStatus.InProgress,
                        existing,
                        ownerId);
                }

                await Task.Delay(_options.PollMilliseconds, cancellationToken);
            }
        }

        private static bool IsUniqueViolation(DbUpdateException exception) =>
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            };

        private async Task DeleteExpiredTerminalRecordsAsync(
            DateTime now,
            CancellationToken cancellationToken)
        {
            await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken);
            await db.CheckoutIdempotencyRecords
                .Where(record => record.ExpiresOn.HasValue
                    && record.ExpiresOn <= now
                    && (record.State == CheckoutIdempotencyState.Completed
                        || record.State == CheckoutIdempotencyState.Failed))
                .ExecuteDeleteAsync(cancellationToken);
        }

        private static string Serialize(PersistedCheckoutOutcome outcome) =>
            JsonSerializer.Serialize(outcome, SerializerOptions);

        private static string CreateOrderReference(Guid paymentMethodId, DateTime now)
        {
            var prefix = paymentMethodId == PaymentMethodIds.CashOnDelivery
                ? "COD"
                : paymentMethodId == PaymentMethodIds.BankTransfer
                    ? "BT"
                    : paymentMethodId == PaymentMethodIds.CreditCard
                        ? "STRIPE"
                        : "CHK";
            return $"{prefix}-{now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";
        }
    }
}
