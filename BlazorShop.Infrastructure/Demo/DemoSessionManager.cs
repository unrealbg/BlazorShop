namespace BlazorShop.Infrastructure.Demo
{
    using System.Collections.Concurrent;
    using System.Security.Cryptography;

    using BlazorShop.Application.Options;
    using BlazorShop.Domain.Entities.Identity;

    using Microsoft.AspNetCore.Identity;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    using Npgsql;

    public sealed class DemoSessionManager : IDemoSessionManager, IAsyncDisposable
    {
        private static readonly string[] BaselineTables =
        [
            "AspNetRoles",
            "AdminSettings",
            "Categories",
            "PaymentMethods",
            "Products",
            "ProductVariants",
            "SeoRedirects",
            "SeoSettings",
        ];

        private readonly ConcurrentDictionary<string, SessionState> _sessions = new(StringComparer.Ordinal);
        private readonly string _connectionString;
        private readonly DemoOptions _options;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly DemoRequestContext _requestContext;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly ILogger<DemoSessionManager> _logger;

        public DemoSessionManager(
            IConfiguration configuration,
            IOptions<DemoOptions> options,
            IServiceScopeFactory scopeFactory,
            DemoRequestContext requestContext,
            IHostEnvironment hostEnvironment,
            ILogger<DemoSessionManager> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("A database connection is required for demo sessions.");
            _options = options.Value;
            _scopeFactory = scopeFactory;
            _requestContext = requestContext;
            _hostEnvironment = hostEnvironment;
            _logger = logger;
        }

        public bool IsEnabled => _options.Enabled;

        public async Task<DemoSessionStartResult> CreateAsync(CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
            {
                throw new InvalidOperationException("Demo sessions are disabled.");
            }

            await CleanupExpiredAsync(cancellationToken);

            if (_sessions.Count >= _options.MaxConcurrentSessions)
            {
                throw new DemoSessionCapacityException();
            }

            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var id = Guid.NewGuid().ToString("N");
            var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.LifetimeMinutes);
            var connection = new NpgsqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);

            try
            {
                await CreateTemporaryDatabaseAsync(connection, cancellationToken);

                var context = new DemoSessionContext(id, token, connection, expiresAtUtc);
                var state = new SessionState(context, TimeSpan.FromMinutes(_options.LifetimeMinutes));

                if (!_sessions.TryAdd(token, state))
                {
                    throw new InvalidOperationException("Unable to register the demo session.");
                }

                try
                {
                    await ProvisionUsersAsync(context, cancellationToken);
                }
                catch
                {
                    _sessions.TryRemove(token, out _);
                    throw;
                }

                _logger.LogInformation("Created isolated demo session {DemoSessionId} expiring at {ExpiresAtUtc}.", id, expiresAtUtc);
                return new DemoSessionStartResult(token, expiresAtUtc);
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        public async ValueTask<DemoSessionLease?> AcquireAsync(string token, CancellationToken cancellationToken = default)
        {
            if (!IsEnabled || string.IsNullOrWhiteSpace(token) || !_sessions.TryGetValue(token, out var state))
            {
                return null;
            }

            if (state.IsExpired)
            {
                await EndAsync(token, cancellationToken);
                return null;
            }

            await state.Gate.WaitAsync(cancellationToken);

            if (state.IsClosing || state.IsExpired)
            {
                state.Gate.Release();
                return null;
            }

            state.Touch();
            return new DemoSessionLease(
                state.Context,
                () =>
                {
                    state.Touch();
                    state.Gate.Release();
                });
        }

        public async Task EndAsync(string token, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token) || !_sessions.TryRemove(token, out var state))
            {
                return;
            }

            state.BeginClosing();
            await state.Gate.WaitAsync(cancellationToken);

            try
            {
                await state.Context.Connection.DisposeAsync();
                DeleteDemoUploads(state.Context.Id);
                _logger.LogInformation("Reset isolated demo session {DemoSessionId}.", state.Context.Id);
            }
            finally
            {
                state.Gate.Release();
                state.Gate.Dispose();
            }
        }

        public async Task CleanupExpiredAsync(CancellationToken cancellationToken = default)
        {
            var expiredTokens = _sessions
                .Where(pair => pair.Value.IsExpired)
                .Select(pair => pair.Key)
                .ToArray();

            foreach (var token in expiredTokens)
            {
                await EndAsync(token, cancellationToken);
            }
        }

        public void RegisterUploadedFile(string sessionId, string filePath)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            var state = _sessions.Values.FirstOrDefault(item => string.Equals(item.Context.Id, sessionId, StringComparison.Ordinal));
            state?.UploadedFiles.Add(filePath);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var token in _sessions.Keys.ToArray())
            {
                await EndAsync(token);
            }
        }

        private static async Task CreateTemporaryDatabaseAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
        {
            await using var createCommand = connection.CreateCommand();
            createCommand.CommandText = """
                SET search_path TO pg_temp, public;
                DO $demo$
                DECLARE current_table text;
                BEGIN
                    FOR current_table IN
                        SELECT tablename
                        FROM pg_tables
                        WHERE schemaname = 'public'
                          AND tablename <> '__EFMigrationsHistory'
                        ORDER BY tablename
                    LOOP
                        EXECUTE format(
                            'CREATE TEMP TABLE %I (LIKE public.%I INCLUDING ALL) ON COMMIT PRESERVE ROWS',
                            current_table,
                            current_table);
                    END LOOP;
                END
                $demo$;
                """;
            await createCommand.ExecuteNonQueryAsync(cancellationToken);

            foreach (var tableName in BaselineTables)
            {
                var quotedTable = new NpgsqlCommandBuilder().QuoteIdentifier(tableName);
                await using var copyCommand = connection.CreateCommand();
                copyCommand.CommandText = $"INSERT INTO pg_temp.{quotedTable} SELECT * FROM public.{quotedTable};";
                await copyCommand.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        private async Task ProvisionUsersAsync(DemoSessionContext context, CancellationToken cancellationToken)
        {
            using var activation = _requestContext.Activate(context);
            await using var scope = _scopeFactory.CreateAsyncScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();

            await CreateUserAsync(userManager, _options.CustomerEmail, "Demo Customer", "User", cancellationToken);
            await CreateUserAsync(userManager, _options.AdminEmail, "Demo Administrator", "Admin", cancellationToken);
        }

        private async Task CreateUserAsync(
            UserManager<AppUser> userManager,
            string email,
            string fullName,
            string role,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var user = new AppUser
            {
                Id = Guid.NewGuid().ToString(),
                Email = email,
                UserName = email,
                FullName = fullName,
                EmailConfirmed = true,
                CreatedOn = DateTime.UtcNow,
            };

            var createResult = await userManager.CreateAsync(user, _options.Password);
            if (!createResult.Succeeded)
            {
                throw new InvalidOperationException($"Unable to create the {role} demo user: {string.Join("; ", createResult.Errors.Select(error => error.Description))}");
            }

            var roleResult = await userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException($"Unable to assign the {role} demo role: {string.Join("; ", roleResult.Errors.Select(error => error.Description))}");
            }
        }

        private void DeleteDemoUploads(string sessionId)
        {
            var demoUploadsRoot = Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, "uploads", "demo"));
            var sessionUploadsPath = Path.GetFullPath(Path.Combine(demoUploadsRoot, sessionId));
            var expectedPrefix = demoUploadsRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!sessionUploadsPath.StartsWith(expectedPrefix, StringComparison.Ordinal)
                || !Directory.Exists(sessionUploadsPath))
            {
                return;
            }

            Directory.Delete(sessionUploadsPath, recursive: true);
        }

        private sealed class SessionState
        {
            private int _closing;
            private readonly TimeSpan _lifetime;

            public SessionState(DemoSessionContext context, TimeSpan lifetime)
            {
                Context = context;
                _lifetime = lifetime;
            }

            public DemoSessionContext Context { get; }

            public SemaphoreSlim Gate { get; } = new(1, 1);

            public ConcurrentBag<string> UploadedFiles { get; } = [];

            public bool IsClosing => Volatile.Read(ref _closing) != 0;

            public bool IsExpired => DateTime.UtcNow >= Context.ExpiresAtUtc;

            public void BeginClosing()
            {
                Interlocked.Exchange(ref _closing, 1);
            }

            public void Touch()
            {
                Context.Extend(_lifetime);
            }
        }
    }

    public sealed class DemoSessionCapacityException : Exception
    {
        public DemoSessionCapacityException()
            : base("All demo workspaces are currently in use. Please try again shortly.")
        {
        }
    }
}
