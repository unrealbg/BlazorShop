namespace BlazorShop.Application.Services.Contracts
{
    using BlazorShop.Application.DTOs;

    public interface INewsletterService
    {
        Task<ServiceResponse> SubscribeAsync(string email);
    }
}
