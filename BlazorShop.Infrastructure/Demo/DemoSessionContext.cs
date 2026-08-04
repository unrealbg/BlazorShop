namespace BlazorShop.Infrastructure.Demo
{
    using Npgsql;

    public sealed class DemoSessionContext
    {
        private long _expiresAtUtcTicks;

        internal DemoSessionContext(string id, string token, NpgsqlConnection connection, DateTime expiresAtUtc)
        {
            Id = id;
            Token = token;
            Connection = connection;
            _expiresAtUtcTicks = expiresAtUtc.Ticks;
        }

        public string Id { get; }

        internal string Token { get; }

        internal NpgsqlConnection Connection { get; }

        public DateTime ExpiresAtUtc => new(Interlocked.Read(ref _expiresAtUtcTicks), DateTimeKind.Utc);

        internal void Extend(TimeSpan lifetime)
        {
            Interlocked.Exchange(ref _expiresAtUtcTicks, DateTime.UtcNow.Add(lifetime).Ticks);
        }
    }
}
