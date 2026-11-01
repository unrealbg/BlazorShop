namespace BlazorShop.Infrastructure.Services;

using System.Security.Cryptography;
using System.Text;
using BlazorShop.Application.Options;
using BlazorShop.Application.Services.Contracts.Authentication;
using BlazorShop.Application.Services.Contracts.Demo;
using BlazorShop.Domain.Contracts.Authentication;
using BlazorShop.Domain.Entities.Identity;
using BlazorShop.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

public sealed class RegistrationConfirmationStore(
    IServiceScopeFactory scopes,
    IDbContextFactory<AppDbContext> contexts,
    IDemoRequestContext demo,
    TimeProvider clock,
    IOptions<RegistrationConfirmationOptions> options,
    ILogger<RegistrationConfirmationStore> logger) : IRegistrationConfirmationStore
{
    public async Task<RegistrationCommitResult> RegisterCustomerAsync(AppUser input)
    {
        // Demo workspaces are temporary and must never enqueue external confirmation mail.
        if (demo.IsDemo) return RegistrationCommitResult.DemoNotSupported;
        var userId = Guid.NewGuid().ToString();
        var eventId = Guid.NewGuid();
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<IAppUserManager>();
        var roles = scope.ServiceProvider.GetRequiredService<IAppRoleManager>();

        // Participate in the configured EF strategy, but consume storage errors inside
        // the delegate: no automatic replay of tracked Identity entities/password hashing.
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync();
                var user = new AppUser
                {
                    Id = userId, Email = input.Email, UserName = input.Email,
                    FullName = input.FullName, PasswordHash = input.PasswordHash, EmailConfirmed = false,
                };
                if (!await users.CreateUserAsync(user)) return RegistrationCommitResult.Duplicate;
                if (!await roles.AddUserToRoleAsync(user, "User")) throw new InvalidOperationException("Registration role assignment failed.");
                db.RegistrationConfirmationDeliveries.Add(NewDelivery(user, eventId, clock.GetUtcNow().UtcDateTime));
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                return RegistrationCommitResult.Created;
            }
            catch (Exception)
            {
                // Disposal rolls back an uncommitted transaction. A lost commit ACK is
                // verified by this request's private event identity, never by email alone.
                if (await EventCommittedAsync(userId, eventId, registration: true)) return RegistrationCommitResult.Created;
                logger.LogWarning("Registration persistence could not be confirmed for operation {OperationId}.", eventId);
                return RegistrationCommitResult.Unavailable;
            }
        });
    }

    public async Task<ConfirmationRequestResult> RequestDeliveryAsync(string userId, string password)
    {
        if (demo.IsDemo) return ConfirmationRequestResult.NotEligible;
        var eventId = Guid.NewGuid();
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync();
                // Account first, outbox second. Serialize explicit resend/recovery without
                // holding a database transaction across SMTP or changing any auth policy.
                var user = await db.Users.FromSqlInterpolated($"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {userId} FOR UPDATE")
                    .SingleOrDefaultAsync();
                if (user is null || user.EmailConfirmed || string.IsNullOrWhiteSpace(user.Email)
                    || await users.IsLockedOutAsync(user) || !await users.CheckPasswordAsync(user, password)
                    || !(await users.GetRolesAsync(user)).Any(role => role is "User" or "Admin"))
                    return ConfirmationRequestResult.NotEligible;

                var now = clock.GetUtcNow().UtcDateTime;
                var row = await db.RegistrationConfirmationDeliveries.SingleOrDefaultAsync(row => row.UserId == userId);
                if (row is null)
                {
                    db.RegistrationConfirmationDeliveries.Add(NewDelivery(user, eventId, now));
                }
                else
                {
                    if (row.State is ConfirmationDeliveryState.Pending or ConfirmationDeliveryState.Processing
                        && MatchesIdentity(row, user, users)) return ConfirmationRequestResult.AlreadyPending;
                    if (now < row.LastRequestedOn.AddSeconds(options.Value.ResendCooldownSeconds))
                        return ConfirmationRequestResult.RateLimited;
                    if (now >= row.RequestWindowStartedOn.AddHours(options.Value.RequestWindowHours))
                    {
                        row.RequestWindowStartedOn = now;
                        row.RequestsInWindow = 0;
                    }
                    if (row.RequestsInWindow >= options.Value.MaxRequestsPerWindow) return ConfirmationRequestResult.RateLimited;
                    row.EventId = eventId;
                    row.Generation = checked(row.Generation + 1);
                    row.EmailSnapshot = user.Email;
                    row.SecurityStampHash = StampHash(user);
                    row.State = ConfirmationDeliveryState.Pending;
                    row.AttemptCount = 0;
                    row.RequestsInWindow++;
                    row.LastRequestedOn = row.UpdatedOn = row.NextAttemptOn = now;
                    row.DeliveredOn = row.LastAttemptOn = row.LeaseExpiresOn = null;
                    row.LeaseOwnerId = null;
                    row.LastFailureCode = null;
                }
                await db.SaveChangesAsync();
                await transaction.CommitAsync();
                return ConfirmationRequestResult.Queued;
            }
            catch (Exception)
            {
                if (await EventCommittedAsync(userId, eventId)) return ConfirmationRequestResult.Queued;
                logger.LogWarning("Confirmation request persistence could not be confirmed for operation {OperationId}.", eventId);
                return ConfirmationRequestResult.Unavailable;
            }
        });
    }

    public async Task<RegistrationConfirmationDelivery?> ClaimAsync(CancellationToken cancellationToken)
    {
        if (demo.IsDemo) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await db.RegistrationConfirmationDeliveries.Where(row =>
            (row.State == ConfirmationDeliveryState.Pending || (row.State == ConfirmationDeliveryState.Processing && row.LeaseExpiresOn <= now))
            && row.AttemptCount >= options.Value.MaxAttempts)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.State, ConfirmationDeliveryState.DeadLetter)
                .SetProperty(row => row.LeaseOwnerId, (Guid?)null).SetProperty(row => row.LeaseExpiresOn, (DateTime?)null)
                .SetProperty(row => row.LastFailureCode, "attempts-exhausted").SetProperty(row => row.UpdatedOn, now), cancellationToken);

        var owner = Guid.NewGuid();
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var command = (NpgsqlCommand)db.Database.GetDbConnection().CreateCommand();
        // One PostgreSQL statement, no execution-strategy replay of an uncertain claim.
        // Unknown outcome means no SMTP; the persisted lease can expire and be recovered.
        command.CommandText = """
            UPDATE "RegistrationConfirmationDeliveries"
            SET "State" = @processing, "LeaseOwnerId" = @owner, "LeaseExpiresOn" = @expires,
                "AttemptCount" = "AttemptCount" + 1, "LastAttemptOn" = @now, "UpdatedOn" = @now
            WHERE "Id" = (
                SELECT "Id" FROM "RegistrationConfirmationDeliveries"
                WHERE (("State" = @pending AND "NextAttemptOn" <= @now)
                    OR ("State" = @processing AND "LeaseExpiresOn" <= @now))
                  AND "AttemptCount" < @max
                ORDER BY "NextAttemptOn", "Id"
                FOR UPDATE SKIP LOCKED LIMIT 1)
            RETURNING "Id"
            """;
        command.Parameters.AddWithValue("processing", (int)ConfirmationDeliveryState.Processing);
        command.Parameters.AddWithValue("pending", (int)ConfirmationDeliveryState.Pending);
        command.Parameters.AddWithValue("owner", owner);
        command.Parameters.AddWithValue("expires", now.AddSeconds(options.Value.LeaseSeconds));
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("max", options.Value.MaxAttempts);
        var id = await command.ExecuteScalarAsync(cancellationToken);
        return id is Guid claimedId
            ? await db.RegistrationConfirmationDeliveries.AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == claimedId && row.LeaseOwnerId == owner, cancellationToken)
            : null;
    }

    public async Task FinishAsync(RegistrationConfirmationDelivery claim, ConfirmationDeliveryState state, string? reason,
        CancellationToken cancellationToken)
    {
        if (demo.IsDemo) return;
        var now = clock.GetUtcNow().UtcDateTime;
        var retrySeconds = Math.Min(options.Value.MaxRetrySeconds,
            options.Value.InitialRetrySeconds * (1L << Math.Min(claim.AttemptCount - 1, 10)));
        if (state == ConfirmationDeliveryState.Pending && claim.AttemptCount >= options.Value.MaxAttempts)
            state = ConfirmationDeliveryState.DeadLetter;
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        await db.RegistrationConfirmationDeliveries
            .Where(row => row.Id == claim.Id && row.EventId == claim.EventId
                && row.State == ConfirmationDeliveryState.Processing && row.LeaseOwnerId == claim.LeaseOwnerId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.State, state)
                .SetProperty(row => row.LastFailureCode, reason).SetProperty(row => row.UpdatedOn, now)
                .SetProperty(row => row.DeliveredOn, state == ConfirmationDeliveryState.Delivered ? now : (DateTime?)null)
                .SetProperty(row => row.NextAttemptOn, now.AddSeconds(retrySeconds))
                .SetProperty(row => row.LeaseOwnerId, (Guid?)null).SetProperty(row => row.LeaseExpiresOn, (DateTime?)null), cancellationToken);
    }

    public static bool MatchesIdentity(RegistrationConfirmationDelivery row, AppUser user, UserManager<AppUser> users) =>
        users.NormalizeEmail(row.EmailSnapshot) == user.NormalizedEmail && row.SecurityStampHash == StampHash(user);

    private static string StampHash(AppUser user)
    {
        if (string.IsNullOrWhiteSpace(user.SecurityStamp)) throw new InvalidOperationException("Registration security stamp unavailable.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user.SecurityStamp)));
    }

    private static RegistrationConfirmationDelivery NewDelivery(AppUser user, Guid eventId, DateTime now) => new()
    {
        Id = eventId, EventId = eventId, UserId = user.Id, EmailSnapshot = user.Email!, SecurityStampHash = StampHash(user),
        CreatedOn = now, UpdatedOn = now, LastRequestedOn = now, RequestWindowStartedOn = now, NextAttemptOn = now,
    };

    private async Task<bool> EventCommittedAsync(string userId, Guid eventId, bool registration = false)
    {
        try
        {
            await using var db = await contexts.CreateDbContextAsync();
            return await db.RegistrationConfirmationDeliveries.AsNoTracking().AnyAsync(row => row.UserId == userId
                && (registration ? row.Id == eventId : row.EventId == eventId));
        }
        catch (Exception) { return false; }
    }
}
