namespace BlazorShop.Tests.Support.Logging
{
    using System.Collections.Concurrent;

    using Microsoft.Extensions.Logging;

    public sealed class TestLogSink
    {
        private readonly ConcurrentQueue<TestLogEntry> _entries = new();

        public IReadOnlyCollection<TestLogEntry> Entries => _entries.ToArray();

        public ILoggerProvider CreateProvider()
        {
            return new TestLoggerProvider(this);
        }

        private void Write(TestLogEntry entry)
        {
            _entries.Enqueue(entry);
        }

        private sealed class TestLoggerProvider : ILoggerProvider
        {
            private readonly TestLogSink _sink;

            public TestLoggerProvider(TestLogSink sink)
            {
                _sink = sink;
            }

            public ILogger CreateLogger(string categoryName)
            {
                return new TestLogger(categoryName, _sink);
            }

            public void Dispose()
            {
            }
        }

        private sealed class TestLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly TestLogSink _sink;

            public TestLogger(string categoryName, TestLogSink sink)
            {
                _categoryName = categoryName;
                _sink = sink;
            }

            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull
            {
                return NullScope.Instance;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var stateValues = state as IEnumerable<KeyValuePair<string, object?>>;
                var properties = stateValues is null
                    ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    : stateValues.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

                _sink.Write(new TestLogEntry(
                    _categoryName,
                    logLevel,
                    eventId,
                    formatter(state, exception),
                    exception,
                    properties));
            }

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();

                public void Dispose()
                {
                }
            }
        }
    }

    public sealed record TestLogEntry(
        string CategoryName,
        LogLevel LogLevel,
        EventId EventId,
        string Message,
        Exception? Exception,
        IReadOnlyDictionary<string, object?> Properties)
    {
        public string? GetString(string propertyName)
        {
            return Properties.TryGetValue(propertyName, out var value)
                ? value?.ToString()
                : null;
        }

        public int? GetInt32(string propertyName)
        {
            if (!Properties.TryGetValue(propertyName, out var value) || value is null)
            {
                return null;
            }

            return value switch
            {
                int intValue => intValue,
                long longValue => checked((int)longValue),
                _ when int.TryParse(value.ToString(), out var parsedValue) => parsedValue,
                _ => null,
            };
        }
    }
}