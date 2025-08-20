namespace BlazorShop.Application.Services.Contracts.Payment
{
    using BlazorShop.Application.DTOs.Payment;

    public interface IOrderQueryService
    {
        Task<IEnumerable<GetOrder>> GetOrdersForUserAsync(string userId);

        Task<IEnumerable<GetOrder>> GetAllAsync();
    }
}
