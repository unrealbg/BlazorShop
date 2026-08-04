namespace BlazorShop.Infrastructure.Demo
{
    public sealed class DemoSessionLease : IAsyncDisposable
    {
        private readonly Action _release;
        private int _disposed;

        internal DemoSessionLease(DemoSessionContext context, Action release)
        {
            Context = context;
            _release = release;
        }

        public DemoSessionContext Context { get; }

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
