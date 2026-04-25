namespace BlazorShop.Infrastructure.Services
{
    using BlazorShop.Application.Services.Contracts;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    public sealed class ApplicationTransactionManager : IApplicationTransactionManager
    {
        private readonly AppDbContext _context;

        public ApplicationTransactionManager(AppDbContext context)
        {
            _context = context;
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action)
        {
            var executionStrategy = _context.Database.CreateExecutionStrategy();

            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    var result = await action();
                    await transaction.CommitAsync();
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }
    }
}