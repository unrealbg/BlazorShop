namespace BlazorShop.Infrastructure.Demo
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    public sealed class DemoSessionCleanupService : BackgroundService
    {
        private readonly IDemoSessionManager _sessionManager;
        private readonly ILogger<DemoSessionCleanupService> _logger;

        public DemoSessionCleanupService(IDemoSessionManager sessionManager, ILogger<DemoSessionCleanupService> logger)
        {
            _sessionManager = sessionManager;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await _sessionManager.CleanupExpiredAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed to clean up expired demo sessions.");
                }
            }
        }
    }
}
