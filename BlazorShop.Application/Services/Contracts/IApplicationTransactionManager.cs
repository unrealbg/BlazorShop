namespace BlazorShop.Application.Services.Contracts
{
    public interface IApplicationTransactionManager
    {
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action);
    }
}