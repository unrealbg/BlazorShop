namespace BlazorShop.Infrastructure
{
    using BlazorShop.Domain.Contracts;
    using BlazorShop.Infrastructure.Data;
    using BlazorShop.Infrastructure.Repositories;

    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
        {
            services.AddDbContext<AppDbContext>(
                opt => opt.UseSqlServer(
                    config.GetConnectionString("DefaultConnection"),
                    sqlOptions =>
                        {
                            sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                            sqlOptions.EnableRetryOnFailure();
                        }));

            services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

            return services;
        }
    }
}
