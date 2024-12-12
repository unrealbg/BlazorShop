namespace BlazorShop.Application.Services.Contracts.Logging
{
    public interface IAppLogger<T>
    {
        void LogInformation(string message);

        void LogWarning(string message);

        void LogError(Exception exception, string message);
    }
}
