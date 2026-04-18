namespace BlazorShop.Infrastructure.Repositories.Seo
{
    using BlazorShop.Domain.Contracts.Seo;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    public class SeoRedirectRepository : ISeoRedirectRepository
    {
        private readonly AppDbContext _context;

        public SeoRedirectRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> OldPathExistsAsync(string oldPath, Guid? excludedRedirectId = null)
        {
            return await _context.SeoRedirects
                .AsNoTracking()
                .AnyAsync(redirect => redirect.OldPath == oldPath
                    && (!excludedRedirectId.HasValue || redirect.Id != excludedRedirectId.Value));
        }
    }
}