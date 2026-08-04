namespace BlazorShop.Infrastructure.Demo
{
    using System.Threading;

    using BlazorShop.Application.Services.Contracts.Demo;

    public sealed class DemoRequestContext : IDemoRequestContext
    {
        private readonly AsyncLocal<RequestState?> _requestState = new();

        public bool IsDemo => Current is not null;

        public string? SessionId => Current?.Id;

        internal DemoSessionContext? Current => _requestState.Value?.Session;

        public bool ShouldEndAfterRequest => _requestState.Value?.EndAfterRequest ?? false;

        public void EndAfterRequest()
        {
            if (_requestState.Value is not null)
            {
                _requestState.Value.EndAfterRequest = true;
            }
        }

        public IDisposable Activate(DemoSessionContext session)
        {
            var previousState = _requestState.Value;
            _requestState.Value = new RequestState(session);
            return new RestoreScope(this, previousState);
        }

        internal RequestState? Capture()
        {
            return _requestState.Value;
        }

        internal void Restore(RequestState? state)
        {
            _requestState.Value = state;
        }

        internal void Set(DemoSessionContext session)
        {
            _requestState.Value = new RequestState(session);
        }

        internal sealed class RequestState
        {
            public RequestState(DemoSessionContext session)
            {
                Session = session;
            }

            public DemoSessionContext Session { get; }

            public bool EndAfterRequest { get; set; }
        }

        private sealed class RestoreScope : IDisposable
        {
            private readonly DemoRequestContext _context;
            private readonly RequestState? _previousState;
            private int _disposed;

            public RestoreScope(DemoRequestContext context, RequestState? previousState)
            {
                _context = context;
                _previousState = previousState;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                {
                    _context._requestState.Value = _previousState;
                }
            }
        }
    }
}
