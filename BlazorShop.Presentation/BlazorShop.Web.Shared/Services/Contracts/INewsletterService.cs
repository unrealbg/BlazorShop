namespace BlazorShop.Web.Shared.Services.Contracts
{
    using BlazorShop.Web.Shared.Models;
    using BlazorShop.Web.Shared.Models.Newsletter;

    public interface INewsletterService
    {
        Task<ServiceResponse> SubscribeAsync(SubscribeRequest request);
    }
}
