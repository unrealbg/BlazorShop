namespace BlazorShop.Infrastructure.Repositories.Newsletters
{
    using System;
    using System.Linq;

    using BlazorShop.Domain.Contracts.Newsletters;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    public class NewsletterSubscriberRepository(AppDbContext context) : INewsletterSubscriberRepository
    {
        private readonly AppDbContext _context = context;

        public async Task<List<NewsletterSubscriber>> GetByDateRangeAsync(DateTime fromUtc, DateTime toUtc)
        {
            return await _context.NewsletterSubscribers
                .AsNoTracking()
                .Where(s => s.CreatedOn >= fromUtc && s.CreatedOn <= toUtc)
                .OrderBy(s => s.CreatedOn)
                .ToListAsync();
        }
    }
}