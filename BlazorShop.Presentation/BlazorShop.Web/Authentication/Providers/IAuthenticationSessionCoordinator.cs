namespace BlazorShop.Web.Authentication.Providers;

public interface IAuthenticationSessionCoordinator
{
    Task<bool> RunAsync(string operation, Func<Task> action, Func<Task>? onIntent = null);
    Task<bool> IsCurrentAsync();
    Task<bool> HasCurrentEpochAsync();
    Task<string> GetEpochAsync();
    Task<bool> IsSignedInAsync(string epoch);
    Task<bool> BeginRefreshAsync();
    Task<bool> CommitTokenAsync(string token);
    Task<bool> InvalidateCurrentAsync();
    Task<bool> PublishSignedInAsync();
}
