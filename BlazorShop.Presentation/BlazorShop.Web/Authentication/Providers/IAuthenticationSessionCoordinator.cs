namespace BlazorShop.Web.Authentication.Providers;

public interface IAuthenticationSessionCoordinator
{
    Task<bool> RunAsync(string operation, Func<Task> action, Func<Task>? onIntent = null);
    Task<bool> IsCurrentAsync();
    Task<bool> HasCurrentEpochAsync();
    Task MarkCurrentEpochAsync();
}
