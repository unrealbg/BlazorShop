namespace BlazorShop.Domain.Contracts.Newsletters
{
    using BlazorShop.Domain.Entities;

    public interface INewsletterSubscriberRepository
    {
        Task<List<NewsletterSubscriber>> GetByDateRangeAsync(DateTime fromUtc, DateTime toUtc);
    }
}