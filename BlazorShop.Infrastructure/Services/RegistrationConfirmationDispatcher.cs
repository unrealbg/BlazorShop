namespace BlazorShop.Infrastructure.Services;

using System.Net;
using BlazorShop.Application.DTOs;
using BlazorShop.Application.Options;
using BlazorShop.Application.Services.Contracts.Demo;
using BlazorShop.Domain.Contracts;
using BlazorShop.Domain.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public sealed class RegistrationConfirmationDispatcher(
    RegistrationConfirmationStore store, IServiceScopeFactory scopes,
    [FromKeyedServices(RegistrationConfirmationDispatcher.EmailServiceKey)] IEmailService sender,
    IOptions<EmailSettings> email, IOptions<ClientAppOptions> client, IOptions<RegistrationConfirmationOptions> options,
    IDemoRequestContext demo, ILogger<RegistrationConfirmationDispatcher> logger)
{
    public const string EmailServiceKey = "registration-confirmation";

    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken = default)
    {
        if (demo.IsDemo || !email.Value.Enabled) return 0; // Disabled delivery is not a successful send.
        var processed = 0;
        while (processed < options.Value.BatchSize && !cancellationToken.IsCancellationRequested)
        {
            var claim = await store.ClaimAsync(cancellationToken);
            if (claim is null) break;
            processed++;
            try
            {
                await using var identityScope = scopes.CreateAsyncScope();
                var users = identityScope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var user = await users.FindByIdAsync(claim.UserId);
                if (user is null || user.EmailConfirmed || await users.IsLockedOutAsync(user)
                    || !RegistrationConfirmationStore.MatchesIdentity(claim, user, users))
                {
                    await store.FinishAsync(claim, ConfirmationDeliveryState.Suppressed, "account-not-eligible", cancellationToken);
                    continue;
                }
                // Retain the existing Identity provider and lifetime. No token/link is stored
                // in the outbox; delayed/retried delivery generates current confirmation data.
                var token = await users.GenerateEmailConfirmationTokenAsync(user);
                var link = $"{client.Value.BaseUrl.TrimEnd('/')}/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";
                cancellationToken.ThrowIfCancellationRequested();
                await sender.SendEmailAsync(claim.EmailSnapshot, "Confirm your email",
                    $"Please confirm your email by clicking <a href=\"{WebUtility.HtmlEncode(link)}\">here</a>.");
                await store.FinishAsync(claim, ConfirmationDeliveryState.Delivered, null, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Uncertain delivery: retain the lease for bounded crash/restart recovery.
                throw;
            }
            catch (Exception)
            {
                // SMTP responses can echo payloads. Never persist/log exception text, token,
                // message body, recipient or transport credentials in this confirmation path.
                logger.LogWarning("Confirmation delivery failed for event {EventId}, attempt {Attempt}.", claim.EventId, claim.AttemptCount);
                await store.FinishAsync(claim, ConfirmationDeliveryState.Pending, "delivery-failure", cancellationToken);
            }
        }
        return processed;
    }
}
