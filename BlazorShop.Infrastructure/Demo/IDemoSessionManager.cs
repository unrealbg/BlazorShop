namespace BlazorShop.Infrastructure.Demo
{
    public interface IDemoSessionManager
    {
        bool IsEnabled { get; }

        Task<DemoSessionStartResult> CreateAsync(CancellationToken cancellationToken = default);

        ValueTask<DemoSessionLease?> AcquireAsync(string token, CancellationToken cancellationToken = default);

        Task EndAsync(string token, CancellationToken cancellationToken = default);

        Task CleanupExpiredAsync(CancellationToken cancellationToken = default);

        void RegisterUploadedFile(string sessionId, string filePath);
    }

    public sealed record DemoSessionStartResult(
        string Token,
        DateTime ExpiresAtUtc);
}
