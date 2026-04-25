namespace BlazorShop.Web.Services
{
    using System.Threading;

    public sealed class AsyncActionGate
    {
        private int _isRunning;

        public bool IsRunning => Interlocked.CompareExchange(ref _isRunning, 0, 0) == 1;

        public async Task<bool> RunAsync(Func<Task> action)
        {
            if (Interlocked.Exchange(ref _isRunning, 1) == 1)
            {
                return false;
            }

            try
            {
                await action();
                return true;
            }
            finally
            {
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }
    }
}