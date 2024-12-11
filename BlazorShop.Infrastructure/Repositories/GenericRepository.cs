﻿namespace BlazorShop.Infrastructure.Repositories
{
    using BlazorShop.Application.Exceptions;
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Infrastructure.Data;

    using Microsoft.EntityFrameworkCore;

    public class GenericRepository<TEntity> : IGenericRepository<TEntity> where TEntity : class
    {
        private readonly AppDbContext _ctx;

        public GenericRepository(AppDbContext ctx)
        {
            _ctx = ctx;
        }

        public async Task<int> AddAsync(TEntity entity)
        {
            _ctx.Set<TEntity>().Add(entity);

            return await _ctx.SaveChangesAsync();
        }

        public async Task<int> DeleteAsync(Guid id)
        {
            var entity = await this.GetByIdAsync(id);

            if (entity == null)
            {
                return 0;
            }

            _ctx.Set<TEntity>().Remove(entity);
            return await _ctx.SaveChangesAsync();
        }

        public async Task<IEnumerable<TEntity>> GetAllAsync()
        {
            return await _ctx.Set<TEntity>().AsNoTracking().ToListAsync();
        }

        public async Task<TEntity> GetByIdAsync(Guid id)
        {
            return await _ctx.Set<TEntity>().FindAsync(id);
        }

        public async Task<int> UpdateAsync(TEntity entity)
        {
            _ctx.Set<TEntity>().Update(entity);
            return await _ctx.SaveChangesAsync();
        }
    }
}
