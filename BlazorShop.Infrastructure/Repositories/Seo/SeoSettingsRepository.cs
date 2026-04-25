namespace BlazorShop.Infrastructure.Repositories.Seo
{
    using BlazorShop.Domain.Contracts.Seo;
    using BlazorShop.Domain.Entities;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    public class SeoSettingsRepository : ISeoSettingsRepository
    {
        private readonly AppDbContext _context;

        public SeoSettingsRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SeoSettings?> GetCurrentAsync()
        {
            return await _context.SeoSettings
                .OrderBy(settings => settings.Id)
                .FirstOrDefaultAsync();
        }
    }
}