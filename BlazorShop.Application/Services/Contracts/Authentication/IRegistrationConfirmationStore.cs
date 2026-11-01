namespace BlazorShop.Application.Services.Contracts.Authentication;

using BlazorShop.Domain.Entities.Identity;

public enum RegistrationCommitResult { Created, Duplicate, Unavailable, DemoNotSupported }
public enum ConfirmationRequestResult { Queued, AlreadyPending, RateLimited, NotEligible, Unavailable }

public interface IRegistrationConfirmationStore
{
    Task<RegistrationCommitResult> RegisterCustomerAsync(AppUser user);
    Task<ConfirmationRequestResult> RequestDeliveryAsync(string userId, string password);
}
